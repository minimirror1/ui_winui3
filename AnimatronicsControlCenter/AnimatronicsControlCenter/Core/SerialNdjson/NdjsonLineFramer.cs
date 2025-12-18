using System;
using System.Collections.Generic;
using System.Text;

namespace AnimatronicsControlCenter.Core.SerialNdjson
{
    /// <summary>
    /// Incrementally frames newline-delimited JSON (NDJSON) lines from arbitrary byte chunks.
    /// </summary>
    public sealed class NdjsonLineFramer
    {
        private readonly int _maxLineBytes;
        private readonly List<byte> _buffer = new();
        private bool _discardingOversize;

        public int OversizeDiscardCount { get; private set; }

        public NdjsonLineFramer(int maxLineBytes)
        {
            if (maxLineBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxLineBytes));
            _maxLineBytes = maxLineBytes;
        }

        /// <summary>
        /// Appends a chunk and returns any complete lines (without trailing CR/LF).
        /// </summary>
        public IReadOnlyList<string> Append(byte[] chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));

            List<string> lines = new();

            for (int i = 0; i < chunk.Length; i++)
            {
                byte b = chunk[i];

                if (_discardingOversize)
                {
                    if (b == (byte)'\n')
                    {
                        _discardingOversize = false;
                        _buffer.Clear();
                    }
                    continue;
                }

                if (b == (byte)'\n')
                {
                    // Strip trailing '\r' if present before decoding.
                    int count = _buffer.Count;
                    if (count > 0 && _buffer[count - 1] == (byte)'\r')
                    {
                        count--;
                    }

                    if (count > 0)
                    {
                        lines.Add(Encoding.UTF8.GetString(_buffer.GetRange(0, count).ToArray()));
                    }
                    else
                    {
                        // Empty line - emit empty string? NDJSON typically doesn't use it, skip.
                        // Keep behavior conservative.
                    }

                    _buffer.Clear();
                    continue;
                }

                _buffer.Add(b);

                if (_buffer.Count > _maxLineBytes)
                {
                    OversizeDiscardCount++;
                    _discardingOversize = true;
                    _buffer.Clear();
                }
            }

            return lines;
        }
    }
}

