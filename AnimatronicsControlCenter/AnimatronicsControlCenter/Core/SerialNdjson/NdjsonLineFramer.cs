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

using System;
using System.Collections.Generic;
using System.Text;

namespace AnimatronicsControlCenter.Core.SerialNdjson;

/// <summary>
/// Incrementally frames newline-delimited UTF-8 text (NDJSON) from arbitrary byte chunks.
/// One frame is defined as bytes up to '\n' (excluding). Optional trailing '\r' is stripped.
/// Oversize frames are discarded until the next '\n'.
/// </summary>
public sealed class NdjsonLineFramer
{
    private enum State
    {
        Accumulating = 0,
        DiscardingOversize = 1,
    }

    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly int _maxLineBytes;
    private readonly byte[] _buffer;

    private State _state;
    private int _count;

    public NdjsonLineFramer(int maxLineBytes)
    {
        if (maxLineBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxLineBytes));

        _maxLineBytes = maxLineBytes;
        _buffer = new byte[maxLineBytes];
        _state = State.Accumulating;
        _count = 0;
    }

    public long OversizeDiscardCount { get; private set; }

    public long InvalidUtf8DiscardCount { get; private set; }

    /// <summary>
    /// Appends a chunk and returns any completed lines.
    /// Empty lines are ignored.
    /// </summary>
    public IReadOnlyList<string> Append(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length == 0) return Array.Empty<string>();

        List<string>? lines = null;

        for (var i = 0; i < chunk.Length; i++)
        {
            var b = chunk[i];

            if (_state == State.Accumulating)
            {
                if (b == (byte)'\n')
                {
                    if (_count == 0)
                    {
                        // ignore empty line
                        continue;
                    }

                    // Strip optional trailing '\r'
                    var len = _count;
                    if (len > 0 && _buffer[len - 1] == (byte)'\r')
                        len--;

                    if (len > 0)
                    {
                        try
                        {
                            var line = Utf8Strict.GetString(_buffer, 0, len);
                            (lines ??= new List<string>(capacity: 2)).Add(line);
                        }
                        catch (DecoderFallbackException)
                        {
                            InvalidUtf8DiscardCount++;
                        }
                    }

                    _count = 0;
                }
                else
                {
                    if (_count >= _maxLineBytes)
                    {
                        OversizeDiscardCount++;
                        _count = 0;
                        _state = State.DiscardingOversize;
                        continue;
                    }

                    _buffer[_count++] = b;
                }
            }
            else
            {
                // DiscardingOversize
                if (b == (byte)'\n')
                {
                    _state = State.Accumulating;
                }
            }
        }

        return (IReadOnlyList<string>?)lines ?? Array.Empty<string>();
    }
}
