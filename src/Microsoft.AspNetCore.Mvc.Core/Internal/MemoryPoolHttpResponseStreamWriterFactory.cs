// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    /// <summary>
    /// An <see cref="IHttpResponseStreamWriterFactory"/> that uses pooled buffers.
    /// </summary>
    public class MemoryPoolHttpResponseStreamWriterFactory : IHttpResponseStreamWriterFactory
    {
        private static readonly string _defaultBufferSizeString = Environment.GetEnvironmentVariable("Mvc_ResponseStreamBufferSize");

        /// <summary>
        /// The default size of created char buffers.
        /// </summary>
        /// <example>4K (char) results in a 16KB (byte) array for UTF8.</example>
        /// <remarks>Defaults to <see cref="HttpResponseStreamWriter.DefaultBufferSize"/>.</remarks>
        public static readonly int DefaultBufferSize = string.IsNullOrEmpty(_defaultBufferSizeString) ?
            HttpResponseStreamWriter.DefaultBufferSize :
            int.Parse(_defaultBufferSizeString);

        private readonly ArrayPool<byte> _bytePool;
        private readonly ArrayPool<char> _charPool;

        /// <summary>
        /// Creates a new <see cref="MemoryPoolHttpResponseStreamWriterFactory"/>.
        /// </summary>
        /// <param name="bytePool">
        /// The <see cref="ArrayPool{Byte}"/> for creating <see cref="byte"/> buffers.
        /// </param>
        /// <param name="charPool">
        /// The <see cref="ArrayPool{Char}"/> for creating <see cref="char"/> buffers.
        /// </param>
        public MemoryPoolHttpResponseStreamWriterFactory(
            ArrayPool<byte> bytePool,
            ArrayPool<char> charPool)
        {
            if (bytePool == null)
            {
                throw new ArgumentNullException(nameof(bytePool));
            }

            if (charPool == null)
            {
                throw new ArgumentNullException(nameof(charPool));
            }

            _bytePool = bytePool;
            _charPool = charPool;
        }

        /// <inheritdoc />
        public TextWriter CreateWriter(Stream stream, Encoding encoding)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            return new HttpResponseStreamWriter(stream, encoding, DefaultBufferSize, _bytePool, _charPool);
        }
    }
}
