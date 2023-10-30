/*
 * Copyright 2021-2023 nurhafiz@hotmail.sg
 *
 * Licensed under the Apache License, version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Toimik.WarcProtocol;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class LineReader : IDisposable
{
    public LineReader(Stream stream, CancellationToken cancellationToken)
    {
        Stream = stream;
        Reader = PipeReader.Create(stream);
        CancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }

    private Stream Stream { get; }

    private PipeReader Reader { get; }

    public void Dispose()
    {
        Reader.Complete();
    }

    public async Task Offset(long byteOffset)
    {
        while (byteOffset > 0)
        {
            var readResult = await Reader.ReadAsync(CancellationToken);

            if (readResult.Buffer.Length == 0 && readResult.IsCompleted)
            {
                throw new ArgumentException("Offset exceeds file size.", nameof(byteOffset));
            }

            long amountToAdvance = Math.Min(byteOffset, readResult.Buffer.Length);
            Reader.AdvanceTo(readResult.Buffer.GetPosition(amountToAdvance));
            byteOffset -= amountToAdvance;
        }
    }

    public async Task<byte[]> ReadBytes(int bytesToRead)
    {
        var readResult = await Reader.ReadAtLeastAsync(bytesToRead);
        if (readResult.Buffer.Length < bytesToRead)
        {
            throw new FormatException("End of file");
        }

        byte[] ret = readResult.Buffer.Slice(0, bytesToRead).ToArray();
        Reader.AdvanceTo(readResult.Buffer.GetPosition(bytesToRead));
        return ret;
    }

    private string? TryReadLine(ReadOnlySequence<byte> buffer, ref long lookedAt)
    {
        var seqReader = new SequenceReader<byte>(buffer);
        seqReader.Advance(lookedAt);
        while (seqReader.TryReadTo(out ReadOnlySpan<byte> data, WarcParser.LineFeed))
        {
            if (data.Length != 0 && data[data.Length - 1] == WarcParser.CarriageReturn)
            {
                var consumed = seqReader.Consumed;
                seqReader.Rewind(2);
                string ret = Encoding.UTF8.GetString(buffer.Slice(0, seqReader.Consumed));
                Reader.AdvanceTo(buffer.GetPosition(consumed));
                return ret;
            }
        }

        lookedAt = seqReader.Consumed;

        return null;
    }

    public async Task<string?> Read()
    {
        // NOTE: A line is terminated by consecutive occurrences of the EOL characters
        long lookedAt = 0;
        while (true)
        {
            var readResult = await Reader.ReadAsync(CancellationToken);

            // TODO: figure out if this is really the EOF case
            if (readResult.IsCompleted)
            {
                if (readResult.Buffer.Length == 0)
                {
                    return null;
                }
                else
                {
                    string ret = Encoding.UTF8.GetString(readResult.Buffer);
                    Reader.AdvanceTo(readResult.Buffer.End);
                    return ret;
                }
            }

            string? line = TryReadLine(readResult.Buffer, ref lookedAt);

            if (line != null)
            {
                return line;
            }

            Reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
        }
    }
}