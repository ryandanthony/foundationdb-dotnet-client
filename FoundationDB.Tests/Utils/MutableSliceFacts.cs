﻿#region BSD License
/* Copyright (c) 2013-2019, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace Doxense.Memory.Tests
{
	//README:IMPORTANT! This source file is expected to be stored as UTF-8! If the encoding is changed, some tests below may fail because they rely on specific code points!

	using NUnit.Framework;
	using System;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Text;
	using FoundationDB.Client.Tests;

	[TestFixture]
	public class MutableSliceFacts : FdbTest
	{

		[Test]
		public void Test_MutableSlice_Nil()
		{
			// MutableSlice.Nil is the equivalent of 'default(byte[])'

			Assert.That(MutableSlice.Nil.Count, Is.EqualTo(0));
			Assert.That(MutableSlice.Nil.Offset, Is.EqualTo(0));
			Assert.That(MutableSlice.Nil.Array, Is.Null);

			Assert.That(MutableSlice.Nil.IsNull, Is.True);
			Assert.That(MutableSlice.Nil.HasValue, Is.False);
			Assert.That(MutableSlice.Nil.IsEmpty, Is.False);
			Assert.That(MutableSlice.Nil.IsNullOrEmpty, Is.True);
			Assert.That(MutableSlice.Nil.IsPresent, Is.False);

			Assert.That(MutableSlice.Nil.GetBytes(), Is.Null);
			Assert.That(MutableSlice.Nil.GetBytesOrEmpty(), Is.Not.Null.And.Length.EqualTo(0));
			Assert.That(MutableSlice.Nil.ToByteString(), Is.Null);
			Assert.That(MutableSlice.Nil.ToUnicode(), Is.Null);
			Assert.That(MutableSlice.Nil.PrettyPrint(), Is.EqualTo(String.Empty));
		}

		[Test]
		public void Test_MutableSlice_Empty()
		{
			// MutableSlice.Empty is the equivalent of 'new byte[0]'

			Assert.That(MutableSlice.Empty.Count, Is.EqualTo(0));
			Assert.That(MutableSlice.Empty.Offset, Is.EqualTo(0));
			Assert.That(MutableSlice.Empty.Array, Is.Not.Null);
			Assert.That(MutableSlice.Empty.Array.Length, Is.GreaterThan(0), "The backing array for MutableSlice.Empty should not be empty, in order to work properly with the fixed() operator!");

			Assert.That(MutableSlice.Empty.IsNull, Is.False);
			Assert.That(MutableSlice.Empty.HasValue, Is.True);
			Assert.That(MutableSlice.Empty.IsEmpty, Is.True);
			Assert.That(MutableSlice.Empty.IsNullOrEmpty, Is.True);
			Assert.That(MutableSlice.Empty.IsPresent, Is.False);

			Assert.That(MutableSlice.Empty.GetBytes(), Is.Not.Null.And.Length.EqualTo(0));
			Assert.That(MutableSlice.Empty.GetBytesOrEmpty(), Is.Not.Null.And.Length.EqualTo(0));
			Assert.That(MutableSlice.Empty.ToByteString(), Is.EqualTo(String.Empty));
			Assert.That(MutableSlice.Empty.ToUnicode(), Is.EqualTo(String.Empty));
			Assert.That(MutableSlice.Empty.PrettyPrint(), Is.EqualTo("''"));
		}

		[Test]
		public void Test_MutableSlice_With_Content()
		{
			var slice = MutableSlice.FromStringAscii("ABC");

			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(3));

			Assert.That(slice.IsNull, Is.False);
			Assert.That(slice.HasValue, Is.True);
			Assert.That(slice.IsEmpty, Is.False);
			Assert.That(slice.IsNullOrEmpty, Is.False);
			Assert.That(slice.IsPresent, Is.True);

			Assert.That(slice.GetBytes(), Is.EqualTo(new byte[3] { 65, 66, 67 }));
			Assert.That(slice.GetBytesOrEmpty(), Is.EqualTo(new byte[3] { 65, 66, 67 }));
			Assert.That(slice.ToByteString(), Is.EqualTo("ABC"));
			Assert.That(slice.ToUnicode(), Is.EqualTo("ABC"));
			Assert.That(slice.PrettyPrint(), Is.EqualTo("'ABC'"));
		}

		[Test]
		public void Test_MutableSlice_Create_With_Capacity()
		{
			Assert.That(MutableSlice.Create(0).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(MutableSlice.Create(16).GetBytes(), Is.EqualTo(new byte[16]));

			Assert.That(() => MutableSlice.Create(-1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_MutableSlice_Create_With_Byte_Array()
		{
			Assert.That(default(byte[]).AsSlice().GetBytes(), Is.EqualTo(null));
			Assert.That(new byte[0].AsSlice().GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(new byte[] { 1, 2, 3 }.AsSlice().GetBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));

			// the array return by GetBytes() should not be the same array that was passed to Create !
			byte[] tmp = Guid.NewGuid().ToByteArray(); // create a 16-byte array
			var slice = tmp.AsSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(tmp.Length));
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));

			// create from a slice of the array
			slice = tmp.AsSlice(4, 7);
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(4));
			Assert.That(slice.Count, Is.EqualTo(7));
			var buf = new byte[7];
			Array.Copy(tmp, 4, buf, 0, 7);
			Assert.That(slice.GetBytes(), Is.EqualTo(buf));

			Assert.That(default(byte[]).AsSlice(), Is.EqualTo(MutableSlice.Nil));
			Assert.That(new byte[0].AsSlice(), Is.EqualTo(MutableSlice.Empty));
		}

		[Test]
		public void Test_MutableSlice_Create_Validates_Arguments()
		{
			// null array only allowed with offset=0 and count=0
			// ReSharper disable AssignNullToNotNullAttribute
			Assert.That(() => default(byte[]).AsSlice(0, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => default(byte[]).AsSlice(1, 0), Throws.Nothing, "Count 0 ignores offset");
			Assert.That(() => default(byte[]).AsSlice(1, 1), Throws.InstanceOf<ArgumentException>());
			// ReSharper restore AssignNullToNotNullAttribute

			// empty array only allowed with offset=0 and count=0
			Assert.That(() => new byte[0].AsSlice(0, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[0].AsSlice(1, 0), Throws.Nothing, "Count 0 ignores offset");
			Assert.That(() => new byte[0].AsSlice(1, 1), Throws.InstanceOf<ArgumentException>());

			// last item must fit in the buffer
			Assert.That(() => new byte[3].AsSlice(0, 4), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsSlice(1, 3), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsSlice(3, 1), Throws.InstanceOf<ArgumentException>());

			// negative arguments
			Assert.That(() => new byte[3].AsSlice(-1, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsSlice(0, -1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsSlice(-1, -1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_MutableSlice_Create_With_ArraySegment()
		{
			byte[] tmp = Guid.NewGuid().ToByteArray();

			var slice = new ArraySegment<byte>(tmp).AsMutableSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(tmp.Length));
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));

			slice = new ArraySegment<byte>(tmp, 4, 7).AsMutableSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(4));
			Assert.That(slice.Count, Is.EqualTo(7));
			var buf = new byte[7];
			Array.Copy(tmp, 4, buf, 0, 7);
			Assert.That(slice.GetBytes(), Is.EqualTo(buf));

			Assert.That(default(ArraySegment<byte>).AsSlice(), Is.EqualTo(MutableSlice.Nil));
			Assert.That(new ArraySegment<byte>(new byte[0]).AsSlice(), Is.EqualTo(MutableSlice.Empty));
		}

		[Test]
		public void Test_MutableSlice_Pseudo_Random()
		{
			var rng = new Random();

			MutableSlice slice = MutableSlice.Random(rng, 16);
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(16));
			// can't really test random data, appart from checking that it's not filled with zeroes
			Assert.That(slice.GetBytes(), Is.Not.All.EqualTo(0));

			Assert.That(MutableSlice.Random(rng, 0), Is.EqualTo(MutableSlice.Empty));

			// ReSharper disable once AssignNullToNotNullAttribute
			Assert.That(() => MutableSlice.Random(default(Random), 16), Throws.ArgumentNullException);
			Assert.That(() => MutableSlice.Random(rng, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_MutableSlice_Cryptographic_Random()
		{
			var rng = System.Security.Cryptography.RandomNumberGenerator.Create();

			// normal
			MutableSlice slice = MutableSlice.Random(rng, 16);
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(16));
			// can't really test random data, appart from checking that it's not filled with zeroes
			Assert.That(slice.GetBytes(), Is.Not.All.EqualTo(0));

			// non-zero bytes
			// we can't 100% test that, unless with a lot of iterations...
			for (int i = 0; i < 256; i++)
			{
				Assert.That(
					MutableSlice.Random(rng, 256, nonZeroBytes: true).GetBytes(),
					Is.All.Not.EqualTo(0)
				);
			}

			Assert.That(MutableSlice.Random(rng, 0), Is.EqualTo(MutableSlice.Empty));
			// ReSharper disable once AssignNullToNotNullAttribute
			Assert.That(() => MutableSlice.Random(default(System.Security.Cryptography.RandomNumberGenerator), 16), Throws.ArgumentNullException);
			Assert.That(() => MutableSlice.Random(rng, -1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_MutableSlice_FromStringAscii()
		{
			Assert.That(MutableSlice.FromStringAscii(default(string)).GetBytes(), Is.Null);
			Assert.That(MutableSlice.FromStringAscii(string.Empty).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(MutableSlice.FromStringAscii("A").GetBytes(), Is.EqualTo(new byte[] { 0x41 }));
			Assert.That(MutableSlice.FromStringAscii("AB").GetBytes(), Is.EqualTo(new byte[] { 0x41, 0x42 }));
			Assert.That(MutableSlice.FromStringAscii("ABC").GetBytes(), Is.EqualTo(new byte[] { 0x41, 0x42, 0x43 }));
			Assert.That(MutableSlice.FromStringAscii("ABCD").GetBytes(), Is.EqualTo(new byte[] { 0x41, 0x42, 0x43, 0x44 }));
			Assert.That(MutableSlice.FromStringAscii("\xFF/ABC").GetBytes(), Is.EqualTo(new byte[] { 0xFF, 0x2F, 0x41, 0x42, 0x43 }));
			Assert.That(MutableSlice.FromStringAscii("héllô").GetBytes(), Is.EqualTo(new byte[] { (byte)'h', 0xE9, (byte)'l', (byte)'l', 0xF4 }));
			Assert.That(MutableSlice.FromStringAscii("This is a test of the emergency encoding system").GetBytes(), Is.EqualTo(Encoding.ASCII.GetBytes("This is a test of the emergency encoding system")));

			// if the string contains non-ASCII chars, it would be corrupted so FromAscii() should throw
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			Assert.That(() => MutableSlice.FromStringAscii("hello 世界"), Throws.Exception, "String that contains code points >= 0x80 should throw");
		}

		[Test]
		public void Test_MutableSlice_ToStringAscii()
		{
			Assert.That(MutableSlice.Nil.ToStringAscii(), Is.Null);
			Assert.That(MutableSlice.Empty.ToStringAscii(), Is.EqualTo(String.Empty));
			Assert.That(new byte[] { 0x41 }.AsSlice().ToStringAscii(), Is.EqualTo("A"));
			Assert.That(new byte[] { 0x41, 0x42 }.AsSlice().ToStringAscii(), Is.EqualTo("AB"));
			Assert.That(new byte[] { 0x41, 0x42, 0x43 }.AsSlice().ToStringAscii(), Is.EqualTo("ABC"));
			Assert.That(new byte[] { 0x41, 0x42, 0x43, 0x44 }.AsSlice().ToStringAscii(), Is.EqualTo("ABCD"));
			Assert.That(new byte[] { 0x7F, 0x00, 0x1F }.AsSlice().ToStringAscii(), Is.EqualTo("\x7F\x00\x1F"));
			Assert.That(new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x46 }.AsSlice(2, 3).ToStringAscii(), Is.EqualTo("CDE"));
			Assert.That(Encoding.ASCII.GetBytes("This is a test of the emergency encoding system").AsSlice().ToStringAscii(), Is.EqualTo("This is a test of the emergency encoding system"));

			// If the slice contain anything other than 7+bit ASCII, it should throw!
			Assert.That(() => new byte[] { 0xFF, 0x41, 0x42, 0x43 }.AsSlice().ToStringAscii(), Throws.Exception, "\\xFF is not valid in 7-bit ASCII strings!");
			Assert.That(() => Encoding.Default.GetBytes("héllô").AsSlice().ToStringAscii(), Throws.Exception, "String that contain code points >= 0x80 should trow");
			Assert.That(() => Encoding.UTF8.GetBytes("héllo 世界").AsSlice().ToStringAscii(), Throws.Exception, "String that contains code points >= 0x80 should throw");
		}

		[Test]
		public void Test_MutableSlice_FromByteString()
		{
			Assert.That(MutableSlice.FromByteString(default(string)).GetBytes(), Is.Null);
			Assert.That(MutableSlice.FromByteString(string.Empty).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(MutableSlice.FromByteString("ABC").GetBytes(), Is.EqualTo(new [] { (byte) 'A', (byte) 'B', (byte) 'C' }));
			Assert.That(MutableSlice.FromByteString("\xFF/ABC").GetBytes(), Is.EqualTo(new [] { (byte) 0xFF, (byte) '/', (byte) 'A', (byte) 'B', (byte) 'C' }));
			Assert.That(MutableSlice.FromByteString("héllô").GetBytes(), Is.EqualTo(new byte[] { (byte)'h', 0xE9, (byte)'l', (byte)'l', 0xF4 }));

			// if the caller likes to live dangerously and call, then the data should be corrupted
			var slice = MutableSlice.FromByteString("hello 世界"); // DON'T EVER DO THAT!
			Assume.That('世' & 0xFF, Is.EqualTo(0x16));
			Assume.That('界' & 0xFF, Is.EqualTo(0x4C));
			Assert.That(slice, Is.EqualTo(MutableSlice.Unescape("hello <16><4C>")));
			Assert.That(slice.ToByteString(), Is.EqualTo("hello \x16L"), "non-ASCII chars should be corrupted after decoding");
			Assert.That(slice.Count, Is.EqualTo(8));

		}

		[Test]
		public void Test_MutableSlice_FromStringAnsi()
		{
			//note: FromStringAnsi uses Encoding.Default which varies from system to system! (win-1252, utf8-, ...)

			Assert.That(MutableSlice.FromStringAnsi(default(string)).GetBytes(), Is.Null);
			Assert.That(MutableSlice.FromStringAnsi(string.Empty).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(MutableSlice.FromStringAnsi("ABC").GetBytes(), Is.EqualTo(new byte[] { 0x41, 0x42, 0x43 }));
			Assert.That(MutableSlice.FromStringAnsi("\xFF/ABC").GetBytes(), Is.EqualTo(Encoding.Default.GetBytes("\xFF/ABC")));
			Assert.That(MutableSlice.FromStringAnsi("héllô").GetBytes(), Is.EqualTo(Encoding.Default.GetBytes("héllô"))); //note: this depends on your OS locale!

			// if the string contains chars that are not supported by the Default encoding, it will be corrupted
			var slice = MutableSlice.FromStringAnsi("hello 世界"); // 8 'letters'
			var expected = Encoding.Default.GetBytes("hello 世界");
			Assert.That(slice.GetBytes(), Is.EqualTo(expected)); //note: this depends on your OS locale!
			Assert.That(slice.ToStringAnsi(), Is.EqualTo(Encoding.Default.GetString(expected)));
		}

		[Test]
		public void Test_MutableSlice_ToStringAnsi()
		{
			//note: FromStringAnsi uses Encoding.Default which varies from system to system! (win-1252, utf8-, ...)

			Assert.That(MutableSlice.Nil.ToStringAnsi(), Is.Null);
			Assert.That(MutableSlice.Empty.ToStringAnsi(), Is.EqualTo(String.Empty));
			Assert.That(new[] { (byte) 'A', (byte) 'B', (byte) 'C' }.AsSlice().ToStringAnsi(), Is.EqualTo("ABC"));
			Assert.That(Encoding.Default.GetBytes("héllô").AsSlice().ToStringAnsi(), Is.EqualTo("héllô")); //note: this depends on your OS locale!
			Assert.That(new[] { (byte) 0xFF, (byte) '/', (byte) 'A', (byte) 'B', (byte) 'C' }.AsSlice().ToStringAnsi(), Is.EqualTo(Encoding.Default.GetString(new[] { (byte) 0xFF, (byte) '/', (byte) 'A', (byte) 'B', (byte) 'C' })));
		}

		[Test]
		public void Test_MutableSlice_FromString_Uses_UTF8()
		{
			Assert.That(MutableSlice.FromString(default(string)).GetBytes(), Is.Null);
			Assert.That(MutableSlice.FromString(string.Empty).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(MutableSlice.FromString("ABC").GetBytes(), Is.EqualTo(new byte[] { 0x41, 0x42, 0x43 }));
			Assert.That(MutableSlice.FromString("é").GetBytes(), Is.EqualTo(new byte[] { 0xC3, 0xA9 }));

			// if the string contains UTF-8 characters, it should be encoded properly
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			var slice = MutableSlice.FromString("héllø 世界"); // 8 'letters'
			Assert.That(slice.GetBytes(), Is.EqualTo(Encoding.UTF8.GetBytes("héllø 世界")));
			Assert.That(slice.ToUnicode(), Is.EqualTo("héllø 世界"), "non-ASCII chars should not be corrupted");
			Assert.That(slice.Count, Is.EqualTo(14));

			// UTF8 does not map \xFF or \xFE directly to a single byte (but at least it should round-trip)
			Assert.That(MutableSlice.FromString("\xFF").GetBytes(), Is.EqualTo(new byte[] { 0xC3, 0xBF }));
			Assert.That(MutableSlice.FromString("\xFE").GetBytes(), Is.EqualTo(new byte[] { 0xC3, 0xBE }));
			Assert.That(new byte[] { 0xC3, 0xBF }.AsSlice().ToUnicode(), Is.EqualTo("\xFF"));
			Assert.That(new byte[] { 0xC3, 0xBE }.AsSlice().ToUnicode(), Is.EqualTo("\xFE"));
		}

		[Test]
		public void Test_MutableSlice_FromStringUtf8()
		{
			Assert.That(MutableSlice.FromStringUtf8(default(string)).GetBytes(), Is.Null);
			Assert.That(MutableSlice.FromStringUtf8(string.Empty).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(MutableSlice.FromStringUtf8("ABC").GetBytes(), Is.EqualTo(new byte[] { 0x41, 0x42, 0x43 }));
			Assert.That(MutableSlice.FromStringUtf8("é").GetBytes(), Is.EqualTo(new byte[] { 0xC3, 0xA9 }));

			// if the string contains UTF-8 characters, it should be encoded properly
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			var slice = MutableSlice.FromStringUtf8("héllø 世界"); // 8 'letters'
			Assert.That(slice.GetBytes(), Is.EqualTo(Encoding.UTF8.GetBytes("héllø 世界")));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("héllø 世界"), "non-ASCII chars should not be corrupted");
			Assert.That(slice.ToUnicode(), Is.EqualTo("héllø 世界"), "non-ASCII chars should not be corrupted");
			Assert.That(slice.Count, Is.EqualTo(14));

			// UTF8 does not map \xFF or \xFE directly to a single byte (but at least it should round-trip)
			Assert.That(MutableSlice.FromStringUtf8("\xFF").GetBytes(), Is.EqualTo(new byte[] { 0xC3, 0xBF }));
			Assert.That(MutableSlice.FromStringUtf8("\xFE").GetBytes(), Is.EqualTo(new byte[] { 0xC3, 0xBE }));
			Assert.That(new byte[] { 0xC3, 0xBF }.AsSlice().ToStringUtf8(), Is.EqualTo("\xFF"));
			Assert.That(new byte[] { 0xC3, 0xBF }.AsSlice().ToUnicode(), Is.EqualTo("\xFF"));
			Assert.That(new byte[] { 0xC3, 0xBE }.AsSlice().ToStringUtf8(), Is.EqualTo("\xFE"));
			Assert.That(new byte[] { 0xC3, 0xBE }.AsSlice().ToUnicode(), Is.EqualTo("\xFE"));
		}

		[Test]
		public void Test_MutableSlice_ToStringUtf8()
		{
			Assert.That(MutableSlice.Nil.ToStringUtf8(), Is.Null);
			Assert.That(MutableSlice.Empty.ToStringUtf8(), Is.EqualTo(String.Empty));
			Assert.That(new[] { (byte) 'A', (byte) 'B', (byte) 'C' }.AsSlice().ToStringUtf8(), Is.EqualTo("ABC"));
			Assert.That(Encoding.UTF8.GetBytes("héllô").AsSlice().ToStringUtf8(), Is.EqualTo("héllô")); //note: this depends on your OS locale!
			Assert.That(Encoding.UTF8.GetBytes("世界").AsSlice().ToStringUtf8(), Is.EqualTo("世界"));

			// should remove the bom!
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, (byte) 'A', (byte) 'B', (byte) 'C' }.AsSlice().ToStringUtf8(), Is.EqualTo("ABC"), "BOM should be removed");
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF }.AsSlice().ToStringUtf8(), Is.EqualTo(String.Empty), "BOM should also be removed for empty string");
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, (byte) 'A', (byte) 'B', (byte) 'C' }.AsSlice().ToStringUtf8(), Is.EqualTo("\uFEFFABC"), "Only one BOM should be removed");

			// corrupted UTF-8
			Assert.That(() => new byte[] { 0xEF, 0xBB }.AsSlice().ToStringUtf8(), Throws.Exception, "Partial BOM should fail to decode");
			Assert.That(() => new byte[] { (byte) 'A', 0xc3, 0x28, (byte) 'B' }.AsSlice().ToStringUtf8(), Throws.Exception, "Invalid 2-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xe2, 0x28, 0xa1, (byte) 'B' }.AsSlice().ToStringUtf8(), Throws.Exception, "Invalid 3-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xf0, 0x28, 0x8c, 0x28, (byte) 'B' }.AsSlice().ToStringUtf8(), Throws.Exception, "Invalid 4-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xf0, 0x28, /*..SNIP..*/ }.AsSlice().ToStringUtf8(), Throws.Exception, "Truncated 4-byte sequence");
		}

		[Test]
		public void Test_MutableSlice_FromStringUtf8WithBom()
		{
			Assert.That(MutableSlice.FromStringUtf8WithBom(default(string)).GetBytes(), Is.Null);
			Assert.That(MutableSlice.FromStringUtf8WithBom(string.Empty).GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
			Assert.That(MutableSlice.FromStringUtf8WithBom("ABC").GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0x42, 0x43 }));
			Assert.That(MutableSlice.FromStringUtf8WithBom("é").GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xA9 }));

			// if the string contains UTF-8 characters, it should be encoded properly
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			var slice = MutableSlice.FromStringUtf8WithBom("héllø 世界"); // 8 'letters'
			Assert.That(slice.GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("héllø 世界")).ToArray()));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("héllø 世界"), "The BOM should be removed");
			Assert.That(slice.ToUnicode(), Is.EqualTo("\xFEFFhéllø 世界"), "The BOM should be preserved");
			Assert.That(slice.Count, Is.EqualTo(3 + 14));

			// UTF8 does not map \xFF or \xFE directly to a single byte (but at least it should round-trip)
			Assert.That(MutableSlice.FromStringUtf8WithBom("\xFF").GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBF }));
			Assert.That(MutableSlice.FromStringUtf8WithBom("\xFE").GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBE }));
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBF }.AsSlice().ToStringUtf8(), Is.EqualTo("\xFF"));
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBF }.AsSlice().ToUnicode(), Is.EqualTo("\uFEFF\xFF"));
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBE }.AsSlice().ToStringUtf8(), Is.EqualTo("\xFE"));
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBE }.AsSlice().ToUnicode(), Is.EqualTo("\uFEFF\xFE"));
		}

		[Test]
		public void Test_MutableSlice_FromChar_Uses_UTF8()
		{
			// from 0 to 127 is regular single-byte ASCII
			Assert.That(MutableSlice.FromChar('\0').GetBytes(), Is.EqualTo(new byte[] { 0 }));
			Assert.That(MutableSlice.FromChar('\x01').GetBytes(), Is.EqualTo(new byte[] { 1 }));
			Assert.That(MutableSlice.FromChar('0').GetBytes(), Is.EqualTo(new byte[] { 48 }));
			Assert.That(MutableSlice.FromChar('A').GetBytes(), Is.EqualTo(new byte[] { 65 }));
			Assert.That(MutableSlice.FromChar('a').GetBytes(), Is.EqualTo(new byte[] { 97 }));
			Assert.That(MutableSlice.FromChar('~').GetBytes(), Is.EqualTo(new byte[] { 126 }));
			Assert.That(MutableSlice.FromChar('\x7F').GetBytes(), Is.EqualTo(new byte[] { 127 }));

			// 128 and above is multi-byte UTF-8
			Assert.That(MutableSlice.FromChar('\x80').GetBytes(), Is.EqualTo(new byte[] { 0xC2, 0x80 }));
			Assert.That(MutableSlice.FromChar('é').GetBytes(), Is.EqualTo(new byte[] { 0xC3, 0xA9 }));
			Assert.That(MutableSlice.FromChar('\u221E').GetBytes(), Is.EqualTo(new byte[] { 0xE2, 0x88, 0x9E }));
			Assert.That(MutableSlice.FromChar('\uFFFE').GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBF, 0xBE}));
		}

		#region Signed...

		#region 24-bits

		#region Little-Endian

		[Test]
		public void Test_MutableSlice_ToInt24()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt24(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToInt24(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x34, 0x12, 0x00 }.AsSlice().ToInt24(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToInt24(), Is.EqualTo(0x123456));

			Assert.That(new byte[] { }.AsSlice().ToInt24(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt24(), Is.EqualTo(0));
			Assert.That(new byte[] { 127 }.AsSlice().ToInt24(), Is.EqualTo(127));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt24(), Is.EqualTo(255));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToInt24(), Is.EqualTo(256));
			Assert.That(new byte[] { 255, 127 }.AsSlice().ToInt24(), Is.EqualTo(32767));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt24(), Is.EqualTo(65535));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToInt24(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 255, 255, 127 }.AsSlice().ToInt24(), Is.EqualTo((1 << 23) - 1));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt24(), Is.EqualTo((1 << 24) - 1));

			Assert.That(() => MutableSlice.Create(4).ToInt24(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region Big Endian

		[Test]
		public void Test_MutableSlice_ToInt24BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt24BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToInt24BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToInt24BE(), Is.EqualTo(0x123456));

			Assert.That(new byte[] { }.AsSlice().ToInt24BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt24BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 127 }.AsSlice().ToInt24BE(), Is.EqualTo(127));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt24BE(), Is.EqualTo(255));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToInt24BE(), Is.EqualTo(256));
			Assert.That(new byte[] { 127, 255 }.AsSlice().ToInt24BE(), Is.EqualTo(32767));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt24BE(), Is.EqualTo(65535));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToInt24BE(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 127, 255, 255 }.AsSlice().ToInt24BE(), Is.EqualTo((1 << 23) - 1));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt24BE(), Is.EqualTo((1 << 24) - 1));

			Assert.That(() => MutableSlice.Create(4).ToInt24BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#region 32-bits

		#region Little-Endian

		[Test]
		public void Test_MutableSlice_FromInt32()
		{
			// 32-bit integers should be encoded in little endian, and with 1, 2 or 4 bytes

			void Verify(int value, string expected)
			{
				Assert.That(MutableSlice.FromInt32(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0x12, "12");
			Verify(0x1234, "3412");
			Verify(0x123456, "563412");
			Verify(0x12345678, "78563412");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0001");
			Verify(65535, "FFFF");
			Verify(65536, "000001");
			Verify(16777215, "FFFFFF");
			Verify(16777216, "00000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(int.MinValue, "00000080");
		}

		[Test]
		public void Test_MutableSlice_FromFixed32()
		{
			// FromFixed32 always produce 4 bytes and uses Little Endian

			Assert.That(MutableSlice.FromFixed32(0).GetBytes(), Is.EqualTo(new byte[4]));
			Assert.That(MutableSlice.FromFixed32(1).GetBytes(), Is.EqualTo(new byte[] { 1, 0, 0, 0 }));
			Assert.That(MutableSlice.FromFixed32(256).GetBytes(), Is.EqualTo(new byte[] { 0, 1, 0, 0 }));
			Assert.That(MutableSlice.FromFixed32(65536).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 1, 0 }));
			Assert.That(MutableSlice.FromFixed32(16777216).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 1 }));
			Assert.That(MutableSlice.FromFixed32(short.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 127, 0, 0 }));
			Assert.That(MutableSlice.FromFixed32(int.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 127 }));

			Assert.That(MutableSlice.FromFixed32(-1).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255 }));
			Assert.That(MutableSlice.FromFixed32(-256).GetBytes(), Is.EqualTo(new byte[] { 0, 255, 255, 255 }));
			Assert.That(MutableSlice.FromFixed32(-65536).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
			Assert.That(MutableSlice.FromFixed32(-16777216).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 255 }));
			Assert.That(MutableSlice.FromFixed32(int.MinValue).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 128 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				int x = rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				MutableSlice s = MutableSlice.FromFixed32(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToInt32(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_MutableSlice_ToInt32()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt32(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToInt32(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToInt32(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 0x00 }.AsSlice().ToInt32(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt32(), Is.EqualTo(0x12345678));

			Assert.That(new byte[] { }.AsSlice().ToInt32(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt32(), Is.EqualTo(0));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt32(), Is.EqualTo(255));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToInt32(), Is.EqualTo(256));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt32(), Is.EqualTo(65535));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToInt32(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToInt32(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt32(), Is.EqualTo((1 << 24) - 1));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToInt32(), Is.EqualTo(1 << 24));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToInt32(), Is.EqualTo(int.MaxValue));

			Assert.That(() => MutableSlice.Create(5).ToInt32(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region Big Endian

		[Test]
		public void Test_MutableSlice_FromInt32BE()
		{
			// 32-bit integers should be encoded in little endian, and with 1, 2 or 4 bytes

			void Verify(int value, string expected)
			{
				Assert.That(MutableSlice.FromInt32BE(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0x12, "12");
			Verify(0x1234, "1234");
			Verify(0x123456, "123456");
			Verify(0x12345678, "12345678");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0100");
			Verify(65535, "FFFF");
			Verify(65536, "010000");
			Verify(16777215, "FFFFFF");
			Verify(16777216, "01000000");
			Verify(int.MaxValue, "7FFFFFFF");
			Verify(int.MinValue, "80000000");
		}

		[Test]
		public void Test_MutableSlice_FromFixed32BE()
		{
			// FromFixed32 always produce 4 bytes and uses Little Endian

			Assert.That(MutableSlice.FromFixed32BE(0).GetBytes(), Is.EqualTo(new byte[4]));
			Assert.That(MutableSlice.FromFixed32BE(1).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 1 }));
			Assert.That(MutableSlice.FromFixed32BE(256).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 1, 0 }));
			Assert.That(MutableSlice.FromFixed32BE(65536).GetBytes(), Is.EqualTo(new byte[] { 0, 1, 0, 0 }));
			Assert.That(MutableSlice.FromFixed32BE(16777216).GetBytes(), Is.EqualTo(new byte[] { 1, 0, 0, 0 }));
			Assert.That(MutableSlice.FromFixed32BE(short.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 127, 255 }));
			Assert.That(MutableSlice.FromFixed32BE(int.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 127, 255, 255, 255 }));

			Assert.That(MutableSlice.FromFixed32BE(-1).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255 }));
			Assert.That(MutableSlice.FromFixed32BE(-256).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 0 }));
			Assert.That(MutableSlice.FromFixed32BE(-65536).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 0, 0 }));
			Assert.That(MutableSlice.FromFixed32BE(-16777216).GetBytes(), Is.EqualTo(new byte[] { 255, 0, 0, 0 }));
			Assert.That(MutableSlice.FromFixed32BE(int.MinValue).GetBytes(), Is.EqualTo(new byte[] { 128, 0, 0, 0 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				int x = rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				MutableSlice s = MutableSlice.FromFixed32BE(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToInt32BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_MutableSlice_ToInt32BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt32BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToInt32BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToInt32BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x00, 0x12, 0x34, 0x56 }.AsSlice().ToInt32BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78 }.AsSlice().ToInt32BE(), Is.EqualTo(0x12345678));

			Assert.That(new byte[] { }.AsSlice().ToInt32BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt32BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt32BE(), Is.EqualTo(255));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToInt32BE(), Is.EqualTo(256));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt32BE(), Is.EqualTo(65535));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToInt32BE(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 0, 1, 0, 0 }.AsSlice().ToInt32BE(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt32BE(), Is.EqualTo((1 << 24) - 1));
			Assert.That(new byte[] { 1, 0, 0, 0 }.AsSlice().ToInt32BE(), Is.EqualTo(1 << 24));
			Assert.That(new byte[] { 127, 255, 255, 255 }.AsSlice().ToInt32BE(), Is.EqualTo(int.MaxValue));

			Assert.That(() => MutableSlice.Create(5).ToInt32BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#region 64-bits

		#region Little-Endian

		[Test]
		public void Test_MutableSlice_FromInt64()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes

			void Verify(long value, string expected)
			{
				Assert.That(MutableSlice.FromInt64(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0x12, "12");
			Verify(0x1234, "3412");
			Verify(0x123456, "563412");
			Verify(0x12345678, "78563412");
			Verify(0x123456789A, "9A78563412");
			Verify(0x123456789ABC, "BC9A78563412");
			Verify(0x123456789ABCDE, "DEBC9A78563412");
			Verify(0x123456789ABCDEF0, "F0DEBC9A78563412");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0001");
			Verify(65535, "FFFF");
			Verify(65536, "000001");
			Verify(16777215, "FFFFFF");
			Verify(16777216, "00000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(int.MinValue, "00000080FFFFFFFF");
			Verify(1L + int.MaxValue, "00000080");
			Verify(long.MaxValue, "FFFFFFFFFFFFFF7F");
			Verify(long.MinValue, "0000000000000080");

		}

		[Test]
		public void Test_MutableSlice_FromFixed64()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			void Verify(long value, byte[] expected)
			{
				Assert.That(MutableSlice.FromFixed64(value).GetBytes(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0L, new byte[8]);
			Verify(1L, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
			Verify(1L << 8, new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 });
			Verify(1L << 16, new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 });
			Verify(1L << 24, new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 });
			Verify(1L << 32, new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 });
			Verify(1L << 40, new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 });
			Verify(1L << 48, new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 });
			Verify(1L << 56, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
			Verify(short.MaxValue, new byte[] { 255, 127, 0, 0, 0, 0, 0, 0 });
			Verify(int.MaxValue, new byte[] { 255, 255, 255, 127, 0, 0, 0, 0 });
			Verify(long.MaxValue, new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 });

			Verify(-1L, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 });
			Verify(-256L, new byte[] { 0, 255, 255, 255, 255, 255, 255, 255 });
			Verify(-65536L, new byte[] { 0, 0, 255, 255, 255, 255, 255, 255 });
			Verify(-16777216L, new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 });
			Verify(-4294967296L, new byte[] { 0, 0, 0, 0, 255, 255, 255, 255 });
			Verify(long.MinValue, new byte[] { 0, 0, 0, 0, 0, 0, 0, 128 });

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				long x = (long)rnd.Next() * rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				MutableSlice s = MutableSlice.FromFixed64(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToInt64(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_MutableSlice_ToInt64()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 0x00 }.AsSlice().ToInt64(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x12345678));
			Assert.That(new byte[] { 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456789A));
			Assert.That(new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456789ABC));
			Assert.That(new byte[] { 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToInt64(), Is.EqualTo(0L));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt64(), Is.EqualTo(0L));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt64(), Is.EqualTo(255L));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(256L));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt64(), Is.EqualTo(65535L));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToInt64(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt64(), Is.EqualTo((1L << 24) - 1));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 24));
			Assert.That(new byte[] { 0, 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 32));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 40));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 48));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 56));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToInt64(), Is.EqualTo(int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }.AsSlice().ToInt64(), Is.EqualTo(long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToInt64(), Is.EqualTo(-1L));

			// should validate the arguments
			var x = new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsMutableSlice();
			Assert.That(() => MutateOffset(x, -1).ToInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 9).ToInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToInt64(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region Big-Endian

		[Test]
		public void Test_MutableSlice_FromInt64BE()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes

			void Verify(long value, string expected)
			{
				Assert.That(MutableSlice.FromInt64BE(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0x12, "12");
			Verify(0x1234, "1234");
			Verify(0x123456, "123456");
			Verify(0x12345678, "12345678");
			Verify(0x123456789A, "123456789A");
			Verify(0x123456789ABC, "123456789ABC");
			Verify(0x123456789ABCDE, "123456789ABCDE");
			Verify(0x123456789ABCDEF0, "123456789ABCDEF0");

			Verify(0, "00");
			Verify(1, "01");
			Verify(127, "7F");
			Verify(128, "80");

			Verify(1L << 8, "0100");
			Verify(1L << 16, "010000");
			Verify(1L << 24, "01000000");
			Verify(1L << 32, "0100000000");
			Verify(1L << 40, "010000000000");
			Verify(1L << 48, "01000000000000");
			Verify(1L << 56, "0100000000000000");

			Verify((1L << 8) - 1, "FF");
			Verify((1L << 16) - 1, "FFFF");
			Verify((1L << 24) - 1, "FFFFFF");
			Verify((1L << 32) - 1, "FFFFFFFF");
			Verify((1L << 40) - 1, "FFFFFFFFFF");
			Verify((1L << 48) - 1, "FFFFFFFFFFFF");
			Verify((1L << 56) - 1, "FFFFFFFFFFFFFF");
			Verify(long.MaxValue, "7FFFFFFFFFFFFFFF");

			Verify(-1, "FFFFFFFFFFFFFFFF");
			Verify(-2, "FFFFFFFFFFFFFFFE");
			Verify(-256, "FFFFFFFFFFFFFF00");
			Verify(-65536, "FFFFFFFFFFFF0000");
			Verify(-16777216, "FFFFFFFFFF000000");
			Verify(int.MinValue, "FFFFFFFF80000000");
			Verify(long.MinValue, "8000000000000000");

		}

		[Test]
		public void Test_MutableSlice_FromFixed64BE()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			void Verify(long value, byte[] expected)
			{
				Assert.That(MutableSlice.FromFixed64BE(value).GetBytes(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0L, new byte[8]);
			Verify(1L, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
			Verify(1L << 8, new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 });
			Verify(1L << 16, new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 });
			Verify(1L << 24, new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 });
			Verify(1L << 32, new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 });
			Verify(1L << 40, new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 });
			Verify(1L << 48, new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 });
			Verify(1L << 56, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
			Verify(short.MaxValue, new byte[] { 0, 0, 0, 0, 0, 0, 127, 255 });
			Verify(int.MaxValue, new byte[] { 0, 0, 0, 0, 127, 255, 255, 255 });
			Verify(long.MaxValue, new byte[] { 127, 255, 255, 255, 255, 255, 255, 255 });

			Verify(-1L, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 });
			Verify(-256L, new byte[] { 255, 255, 255, 255, 255, 255, 255, 0 });
			Verify(-65536L, new byte[] { 255, 255, 255, 255, 255, 255, 0, 0 });
			Verify(-16777216L, new byte[] { 255, 255, 255, 255, 255, 0, 0, 0 });
			Verify(-4294967296L, new byte[] { 255, 255, 255, 255, 0, 0, 0, 0 });
			Verify(long.MinValue, new byte[] { 128, 0, 0, 0, 0, 0, 0, 0 });

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				long x = (long)rnd.Next() * rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				MutableSlice s = MutableSlice.FromFixed64BE(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToInt64BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_MutableSlice_ToInt64BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt64BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToInt64BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x00, 0x12, 0x34, 0x56 }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78 }.AsSlice().ToInt64BE(), Is.EqualTo(0x12345678));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456789A));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456789ABC));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToInt64BE(), Is.EqualTo(0L));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt64BE(), Is.EqualTo(0L));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt64BE(), Is.EqualTo(255L));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(256L));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo(65535L));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 0, 1, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo((1L << 24) - 1));
			Assert.That(new byte[] { 1, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 24));
			Assert.That(new byte[] { 1, 0, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 32));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 40));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 48));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 56));
			Assert.That(new byte[] { 127, 255, 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo(int.MaxValue));
			Assert.That(new byte[] { 127, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo(long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo(-1L));

			// should validate the arguments
			var x = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsMutableSlice();
			Assert.That(() => MutateOffset(x, -1).ToInt64BE(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 9).ToInt64BE(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToInt64BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#endregion

		#region Unsigned...

		#region 32-bits

		#region Little-Endian

		[Test]
		public void Test_MutableSlice_FromUInt32()
		{
			// 32-bit integers should be encoded in little endian, and with 1, 2 or 4 bytes

			void Verify(uint value, string expected)
			{
				Assert.That(MutableSlice.FromUInt32(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0x12, "12");
			Verify(0x1234, "3412");
			Verify(0x123456, "563412");
			Verify(0x12345678, "78563412");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0001");
			Verify(65535, "FFFF");
			Verify(65536, "000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(uint.MaxValue, "FFFFFFFF");
		}

		[Test]
		public void Test_MutableSlice_FromFixedU32()
		{
			// FromFixed32 always produce 4 bytes and uses Little Endian

			void Verify(uint value, byte[] expected)
			{
				Assert.That(MutableSlice.FromFixedU32(value).GetBytes(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0, new byte[4]);
			Verify(1, new byte[] { 1, 0, 0, 0 });
			Verify(256, new byte[] { 0, 1, 0, 0 });
			Verify(ushort.MaxValue, new byte[] { 255, 255, 0, 0 });
			Verify(65536, new byte[] { 0, 0, 1, 0 });
			Verify(16777216, new byte[] { 0, 0, 0, 1 });
			Verify(int.MaxValue, new byte[] { 255, 255, 255, 127 });
			Verify(uint.MaxValue, new byte[] { 255, 255, 255, 255 });

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				uint x = (uint)rnd.Next() + (uint)rnd.Next();
				MutableSlice s = MutableSlice.FromFixedU32(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToUInt32(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_MutableSlice_ToUInt32()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt32(), Is.EqualTo(0x12U));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToUInt32(), Is.EqualTo(0x1234U));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToUInt32(), Is.EqualTo(0x123456U));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 0x00 }.AsSlice().ToUInt32(), Is.EqualTo(0x123456U));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt32(), Is.EqualTo(0x12345678U));

			Assert.That(new byte[] { }.AsSlice().ToUInt32(), Is.EqualTo(0U));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt32(), Is.EqualTo(0U));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt32(), Is.EqualTo(255U));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToUInt32(), Is.EqualTo(256U));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt32(), Is.EqualTo(65535U));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToUInt32(), Is.EqualTo(1U << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToUInt32(), Is.EqualTo(1U << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt32(), Is.EqualTo((1U << 24) - 1U));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToUInt32(), Is.EqualTo(1U << 24));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToUInt32(), Is.EqualTo((uint)int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt32(), Is.EqualTo(uint.MaxValue));

			Assert.That(() => MutableSlice.Create(5).ToUInt32(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region Big-Endian

		[Test]
		public void Test_MutableSlice_FromUInt32BE()
		{
			// 32-bit integers should be encoded in big endian, and with 1, 2 or 4 bytes

			void Verify(uint value, string expected)
			{
				Assert.That(MutableSlice.FromUInt32BE(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0x12, "12");
			Verify(0x1234, "1234");
			Verify(0x123456, "123456");
			Verify(0x12345678, "12345678");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0100");
			Verify(65535, "FFFF");
			Verify(65536, "010000");
			Verify(int.MaxValue, "7FFFFFFF");
			Verify(uint.MaxValue, "FFFFFFFF");
		}

		[Test]
		public void Test_MutableSlice_FromFixedU32BE()
		{
			// FromFixedU32BE always produce 4 bytes and uses Big Endian

			void Verify(uint value, byte[] expected)
			{
				Assert.That(MutableSlice.FromFixedU32BE(value).GetBytes(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0, new byte[4]);
			Verify(1, new byte[] { 0, 0, 0, 1 });
			Verify(256, new byte[] { 0, 0, 1, 0 });
			Verify(ushort.MaxValue, new byte[] { 0, 0, 255, 255 });
			Verify(65536, new byte[] { 0, 1, 0, 0 });
			Verify(16777216, new byte[] { 1, 0, 0, 0 });
			Verify(int.MaxValue, new byte[] { 127, 255, 255, 255 });
			Verify(uint.MaxValue, new byte[] { 255, 255, 255, 255 });

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				uint x = (uint)rnd.Next() + (uint)rnd.Next();
				MutableSlice s = MutableSlice.FromFixedU32BE(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToUInt32BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_MutableSlice_ToUInt32BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x12U));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x1234U));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x123456U));
			Assert.That(new byte[] { 0x00, 0x12, 0x34, 0x56 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x123456U));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x12345678U));

			Assert.That(new byte[] { }.AsSlice().ToUInt32BE(), Is.EqualTo(0U));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(0U));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt32BE(), Is.EqualTo(255U));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(256U));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt32BE(), Is.EqualTo(65535U));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(1U << 16));
			Assert.That(new byte[] { 0, 1, 0, 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(1U << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt32BE(), Is.EqualTo((1U << 24) - 1U));
			Assert.That(new byte[] { 1, 0, 0, 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(1U << 24));
			Assert.That(new byte[] { 127, 255, 255, 255 }.AsSlice().ToUInt32BE(), Is.EqualTo((uint)int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt32BE(), Is.EqualTo(uint.MaxValue));

			Assert.That(() => MutableSlice.Create(5).ToUInt32BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#region 64-bits

		[Test]
		public void Test_MutableSlice_FromUInt64()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes

			void Verify(ulong value, string expected)
			{
				Assert.That(MutableSlice.FromUInt64(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0x12UL, "12");
			Verify(0x1234UL, "3412");
			Verify(0x123456UL, "563412");
			Verify(0x12345678UL, "78563412");
			Verify(0x123456789AUL, "9A78563412");
			Verify(0x123456789ABCUL, "BC9A78563412");
			Verify(0x123456789ABCDEUL, "DEBC9A78563412");
			Verify(0x123456789ABCDEF0UL, "F0DEBC9A78563412");

			Verify(0UL, "00");
			Verify(1UL, "01");
			Verify(255UL, "FF");
			Verify(256UL, "0001");
			Verify(ushort.MaxValue, "FFFF");
			Verify(65536UL, "000001");
			Verify(16777215UL, "FFFFFF");
			Verify(16777216UL, "00000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(16777216UL, "00000001");
			Verify(uint.MaxValue + 1UL, "0000000001");
			Verify(long.MaxValue, "FFFFFFFFFFFFFF7F");
			Verify(ulong.MaxValue, "FFFFFFFFFFFFFFFF");

		}

		[Test]
		public void Test_MutableSlice_FromFixedU64()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			void Verify(ulong value, byte[] expected)
			{
				Assert.That(MutableSlice.FromFixedU64(value).GetBytes(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0UL, new byte[8]);
			Verify(1UL, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
			Verify(1UL << 8, new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 });
			Verify(1UL << 16, new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 });
			Verify(1UL << 24, new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 });
			Verify(1UL << 32, new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 });
			Verify(1UL << 40, new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 });
			Verify(1UL << 48, new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 });
			Verify(1UL << 56, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
			Verify(ushort.MaxValue, new byte[] { 255, 255, 0, 0, 0, 0, 0, 0 });
			Verify(int.MaxValue, new byte[] { 255, 255, 255, 127, 0, 0, 0, 0 });
			Verify(uint.MaxValue, new byte[] { 255, 255, 255, 255, 0, 0, 0, 0 });
			Verify(long.MaxValue, new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 });
			Verify(ulong.MaxValue, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 });

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				ulong x = (ulong)rnd.Next() * (ulong)rnd.Next();
				MutableSlice s = MutableSlice.FromFixedU64(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToUInt64(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_MutableSlice_ToUInt64()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 00 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x12345678));
			Assert.That(new byte[] { 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456789A));
			Assert.That(new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456789ABC));
			Assert.That(new byte[] { 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToUInt64(), Is.EqualTo(0UL));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt64(), Is.EqualTo(0UL));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt64(), Is.EqualTo(255UL));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(256UL));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt64(), Is.EqualTo(65535UL));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt64(), Is.EqualTo((1UL << 24) - 1));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 24));
			Assert.That(new byte[] { 0, 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 32));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 40));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 48));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 56));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToUInt64(), Is.EqualTo(int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt64(), Is.EqualTo(uint.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }.AsSlice().ToUInt64(), Is.EqualTo(long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt64(), Is.EqualTo(ulong.MaxValue));

			// should validate the arguments
			var x = new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsMutableSlice();
			Assert.That(() => MutateOffset(x, -1).ToUInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 5).ToUInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToUInt64(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_MutableSlice_FromUInt64BE()
		{
			// 64-bit integers should be encoded in big endian, and using from 1 to 8 bytes

			void Verify(ulong value, string expected)
			{
				Assert.That(MutableSlice.FromUInt64BE(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0x12UL, "12");
			Verify(0x1234UL, "1234");
			Verify(0x123456UL, "123456");
			Verify(0x12345678UL, "12345678");
			Verify(0x123456789AUL, "123456789A");
			Verify(0x123456789ABCUL, "123456789ABC");
			Verify(0x123456789ABCDEUL, "123456789ABCDE");
			Verify(0x123456789ABCDEF0UL, "123456789ABCDEF0");

			Verify(0UL, "00");
			Verify(1UL, "01");
			Verify(255UL, "FF");
			Verify(256UL, "0100");
			Verify(ushort.MaxValue, "FFFF");
			Verify(65536UL, "010000");
			Verify(16777215UL, "FFFFFF");
			Verify(16777216UL, "01000000");
			Verify(int.MaxValue, "7FFFFFFF");
			Verify(16777216UL, "01000000");
			Verify(uint.MaxValue + 1UL, "0100000000");
			Verify(long.MaxValue, "7FFFFFFFFFFFFFFF");
			Verify(ulong.MaxValue, "FFFFFFFFFFFFFFFF");

		}

		[Test]
		public void Test_MutableSlice_FromFixedU64BE()
		{
			// FromFixed64 always produce 8 bytes and uses Big Endian

			void Verify(ulong value, byte[] expected)
			{
				Assert.That(MutableSlice.FromFixedU64BE(value).GetBytes(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0UL, new byte[8]);
			Verify(1L, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
			Verify(1L << 8, new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 });
			Verify(1L << 16, new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 });
			Verify(1L << 24, new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 });
			Verify(1L << 32, new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 });
			Verify(1L << 40, new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 });
			Verify(1L << 48, new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 });
			Verify(1L << 56, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
			Verify(ushort.MaxValue, new byte[] { 0, 0, 0, 0, 0, 0, 255, 255 });
			Verify(int.MaxValue, new byte[] { 0, 0, 0, 0, 127, 255, 255, 255 });
			Verify(uint.MaxValue, new byte[] { 0, 0, 0, 0, 255, 255, 255, 255 });
			Verify(long.MaxValue, new byte[] { 127, 255, 255, 255, 255, 255, 255, 255 });
			Verify(ulong.MaxValue, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 });

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				ulong x = (ulong)rnd.Next() * (ulong)rnd.Next();
				MutableSlice s = MutableSlice.FromFixedU64BE(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToUInt64BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_MutableSlice_ToUInt64BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x00, 0x12, 0x34, 0x56 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x12345678));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456789A));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456789ABC));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToUInt64BE(), Is.EqualTo(0L));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(0L));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(255L));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(256L));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(65535L));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 0, 1, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo((1L << 24) - 1));
			Assert.That(new byte[] { 1, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 24));
			Assert.That(new byte[] { 1, 0, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 32));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 40));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 48));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 56));
			Assert.That(new byte[] { 127, 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(uint.MaxValue));
			Assert.That(new byte[] { 127, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(ulong.MaxValue));

			// should validate the arguments
			var x = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsMutableSlice();
			Assert.That(() => MutateOffset(x, -1).ToUInt64BE(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 9).ToUInt64BE(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToUInt64BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#region Floating Point...

		private static string SwapHexa(string hexa)
		{
			char[] res = new char[hexa.Length];
			int p = 0;
			for (int i = hexa.Length - 2; i >= 0; i -= 2, p += 2)
			{
				res[i + 0] = hexa[p + 0];
				res[i + 1] = hexa[p + 1];
			}
			return new string(res);
		}

		[Test]
		public void Test_MutableSlice_FromSingle()
		{
			void Verify(float value, string expected)
			{
				Assert.That(MutableSlice.FromSingle(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0} (Little Endian)", value);
				Assert.That(MutableSlice.FromSingleBE(value).ToHexaString(), Is.EqualTo(SwapHexa(expected)), "Invalid encoding for {0} (Big Endian)", value);
			}

			Verify(0f, "00000000");
			Verify(1f, "0000803F");
			Verify(-1f, "000080BF");
			Verify(10f, "00002041");
			Verify(0.1f, "CDCCCC3D");
			Verify(0.5f, "0000003F");

			Verify(1f / 3f, "ABAAAA3E");
			Verify((float) Math.PI, "DB0F4940");
			Verify((float) Math.E, "54F82D40");

			Verify(float.NaN, "0000C0FF");
			Verify(float.Epsilon, "01000000");
			Verify(float.MaxValue, "FFFF7F7F");
			Verify(float.MinValue, "FFFF7FFF");
			Verify(float.PositiveInfinity, "0000807F");
			Verify(float.NegativeInfinity, "000080FF");
		}

		[Test]
		public void Test_MutableSlice_ToSingle()
		{
			void Verify(string value, float expected)
			{
				Assert.That(MutableSlice.FromHexa(value).ToSingle(), Is.EqualTo(expected), "Invalid decoding for '{0}' (Little Endian)", value);
				Assert.That(MutableSlice.FromHexa(SwapHexa(value)).ToSingleBE(), Is.EqualTo(expected), "Invalid decoding for '{0}' (Big Endian)", value);
			}

			Assert.That(MutableSlice.Empty.ToSingle(), Is.EqualTo(0d));
			Verify("00000000", 0f);
			Verify("0000803F", 1f);
			Verify("000080BF", -1f);
			Verify("00002041", 10f);
			Verify("CDCCCC3D", 0.1f);
			Verify("0000003F", 0.5f);

			Verify("ABAAAA3E", 1f / 3f);
			Verify("DB0F4940", (float) Math.PI);
			Verify("54F82D40", (float) Math.E);

			Verify("0000C0FF", float.NaN);
			Verify("01000000", float.Epsilon);
			Verify("FFFF7F7F", float.MaxValue);
			Verify("FFFF7FFF", float.MinValue);
			Verify("0000807F", float.PositiveInfinity);
			Verify("000080FF", float.NegativeInfinity);

			Assert.That(() => MutableSlice.Create(5).ToSingle(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutableSlice.Create(3).ToSingle(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_MutableSlice_FromDouble()
		{
			void Verify(double value, string expected)
			{
				Assert.That(MutableSlice.FromDouble(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0} (Little Endian)", value);
				Assert.That(MutableSlice.FromDoubleBE(value).ToHexaString(), Is.EqualTo(SwapHexa(expected)), "Invalid encoding for {0} (Big Endian)", value);
			}

			Verify(0d, "0000000000000000");
			Verify(1d, "000000000000F03F");
			Verify(-1d, "000000000000F0BF");
			Verify(10d, "0000000000002440");
			Verify(0.1d, "9A9999999999B93F");
			Verify(0.5d, "000000000000E03F");

			Verify(1d / 3d, "555555555555D53F");
			Verify(Math.PI, "182D4454FB210940");
			Verify(Math.E, "6957148B0ABF0540");

			Verify(double.NaN, "000000000000F8FF");
			Verify(double.Epsilon, "0100000000000000");
			Verify(double.MaxValue, "FFFFFFFFFFFFEF7F");
			Verify(double.MinValue, "FFFFFFFFFFFFEFFF");
			Verify(double.PositiveInfinity, "000000000000F07F");
			Verify(double.NegativeInfinity, "000000000000F0FF");

		}

		[Test]
		public void Test_MutableSlice_ToDouble()
		{
			void Verify(string value, double expected)
			{
				Assert.That(MutableSlice.FromHexa(value).ToDouble(), Is.EqualTo(expected), "Invalid decoding for '{0}' (Little Endian)", value);
				Assert.That(MutableSlice.FromHexa(SwapHexa(value)).ToDoubleBE(), Is.EqualTo(expected), "Invalid decoding for '{0}' (Big Endian)", value);
			}

			Verify("", 0d);
			Verify("0000000000000000", 0d);
			Verify("000000000000F03F", 1d);
			Verify("000000000000F0BF", -1d);
			Verify("0000000000002440", 10d);
			Verify("9A9999999999B93F", 0.1d);
			Verify("000000000000E03F", 0.5d);

			Verify("555555555555D53F", 1d / 3d);
			Verify("182D4454FB210940", Math.PI);
			Verify("6957148B0ABF0540", Math.E);

			Verify("000000000000F8FF", double.NaN);
			Verify("0100000000000000", double.Epsilon);
			Verify("FFFFFFFFFFFFEF7F", double.MaxValue);
			Verify("FFFFFFFFFFFFEFFF", double.MinValue);
			Verify("000000000000F07F", double.PositiveInfinity);
			Verify("000000000000F0FF", double.NegativeInfinity);

			Assert.That(() => MutableSlice.Create(9).ToDouble(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutableSlice.Create(7).ToDouble(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_MutableSlice_FromDecimal()
		{
			void Verify(decimal value, string expected)
			{
				Assert.That(MutableSlice.FromDecimal(value).ToHexaString(), Is.EqualTo(expected), "Invalid encoding for {0}", value);
			}

			Verify(0m, "00000000000000000000000000000000");
			Verify(1m, "00000000000000000100000000000000");
			Verify(-1m, "00000080000000000100000000000000");
			Verify(10m, "00000000000000000A00000000000000");
			Verify(0.1m, "00000100000000000100000000000000");
			Verify(0.5m, "00000100000000000500000000000000");

			Verify(1m / 3m, "00001C00CA44C50A55555505CB00B714");
			Verify((decimal) Math.PI, "00000E000000000083246AE7B91D0100");
			Verify((decimal)Math.E, "00000E0000000000D04947EE39F70000");

			Verify(decimal.MaxValue, "00000000FFFFFFFFFFFFFFFFFFFFFFFF");
			Verify(decimal.MinValue, "00000080FFFFFFFFFFFFFFFFFFFFFFFF");

		}

		[Test]
		public void Test_MutableSlice_ToDecimal()
		{
			void Verify(string value, decimal expected)
			{
				Assert.That(MutableSlice.FromHexa(value).ToDecimal(), Is.EqualTo(expected), "Invalid decoding for '{0}'", value);
			}

			Verify("", 0m);
			Verify("00000000000000000000000000000000", 0m);
			Verify("00000000000000000100000000000000", 1m);
			Verify("00000080000000000100000000000000", -1m);
			Verify("00000000000000000A00000000000000", 10m);
			Verify("00000100000000000100000000000000", 0.1m);
			Verify("00000100000000000500000000000000", 0.5m);

			Verify("00001C00CA44C50A55555505CB00B714", 1m / 3m);
			Verify("00000E000000000083246AE7B91D0100", (decimal) Math.PI);
			Verify("00000E0000000000D04947EE39F70000", (decimal) Math.E);

			Verify("00000000FFFFFFFFFFFFFFFFFFFFFFFF", decimal.MaxValue);
			Verify("00000080FFFFFFFFFFFFFFFFFFFFFFFF", decimal.MinValue);

			Assert.That(() => MutableSlice.Create(15).ToDecimal(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutableSlice.Create(17).ToDecimal(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region UUIDs...

		[Test]
		public void Test_MutableSlice_FromGuid()
		{
			// Verify that System.GUID are stored as UUIDs using RFC 4122, and not their natural in-memory format

			// empty guid should be all zeroes
			MutableSlice slice = MutableSlice.FromGuid(Guid.Empty);
			Assert.That(slice.ToHexaString(), Is.EqualTo("00000000000000000000000000000000"));

			// GUIDs should be stored using RFC 4122 (big endian)
			var guid = new Guid("00112233-4455-6677-8899-aabbccddeeff");

			// byte order should follow the string!
			slice = MutableSlice.FromGuid(guid);
			Assert.That(slice.ToHexaString(), Is.EqualTo("00112233445566778899AABBCCDDEEFF"), "MutableSlice.FromGuid() should use the RFC 4122 encoding");

			// but guid in memory should follow MS format
			slice = guid.ToByteArray().AsMutableSlice(); // <-- this is BAD, don't try this at home !
			Assert.That(slice.ToHexaString(), Is.EqualTo("33221100554477668899AABBCCDDEEFF"));
		}

		[Test]
		public void Test_MutableSlice_ToGuid()
		{
			// nil or empty should return Guid.Empty
			Assert.That(MutableSlice.Nil.ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(MutableSlice.Empty.ToGuid(), Is.EqualTo(Guid.Empty));

			// all zeroes should also return Guid.Empty
			MutableSlice slice = MutableSlice.Create(16);
			Assert.That(slice.ToGuid(), Is.EqualTo(Guid.Empty));

			// RFC 4122 encoded UUIDs should be properly reversed when converted to System.GUID
			slice = MutableSlice.FromHexa("00112233445566778899aabbccddeeff");
			Guid guid = slice.ToGuid();
			Assert.That(guid.ToString(), Is.EqualTo("00112233-4455-6677-8899-aabbccddeeff"), "slice.ToGuid() should convert RFC 4122 encoded UUIDs into native System.Guid");

			// round-trip
			guid = Guid.NewGuid();
			Assert.That(MutableSlice.FromGuid(guid).ToGuid(), Is.EqualTo(guid));

			Assert.That(MutableSlice.FromStringAscii(guid.ToString()).ToGuid(), Is.EqualTo(guid), "String literals should also be converted if they match the expected format");

			Assert.That(() => MutableSlice.FromStringAscii("random text").ToGuid(), Throws.InstanceOf<FormatException>());

			// should validate the arguments
			var x = MutableSlice.FromGuid(guid);
			Assert.That(() => MutateOffset(x, -1).ToGuid(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 17).ToGuid(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToGuid(), Throws.InstanceOf<FormatException>());

		}

		[Test]
		public void Test_MutableSlice_FromUuid128()
		{
			// Verify that FoundationDb.Client.Uuid are stored as 128-bit UUIDs using RFC 4122

			// empty guid should be all zeroes
			MutableSlice slice = MutableSlice.FromUuid128(Uuid128.Empty);
			Assert.That(slice.ToHexaString(), Is.EqualTo("00000000000000000000000000000000"));

			// UUIDs should be stored using RFC 4122 (big endian)
			var uuid = new Uuid128("00112233-4455-6677-8899-aabbccddeeff");

			// byte order should follow the string!
			slice = MutableSlice.FromUuid128(uuid);
			Assert.That(slice.ToHexaString(), Is.EqualTo("00112233445566778899AABBCCDDEEFF"), "MutableSlice.FromUuid() should preserve RFC 4122 ordering");

			// ToByteArray() should also be safe
			slice = uuid.ToByteArray().AsMutableSlice();
			Assert.That(slice.ToHexaString(), Is.EqualTo("00112233445566778899AABBCCDDEEFF"));
		}

		[Test]
		public void Test_MutableSlice_ToUuid128()
		{
			// nil or empty should return Uuid128.Empty
			Uuid128 uuid = MutableSlice.Nil.ToUuid128();
			Assert.That(uuid, Is.EqualTo(Uuid128.Empty));
			uuid = MutableSlice.Empty.ToUuid128();
			Assert.That(uuid, Is.EqualTo(Uuid128.Empty));

			// all zeroes should also return Uuid128.Empty
			MutableSlice slice = MutableSlice.Create(16);
			Assert.That(slice.ToUuid128(), Is.EqualTo(Uuid128.Empty));

			// RFC 4122 encoded UUIDs should not keep the byte ordering
			slice = MutableSlice.FromHexa("00112233445566778899aabbccddeeff");
			uuid = slice.ToUuid128();
			Assert.That(uuid.ToString(), Is.EqualTo("00112233-4455-6677-8899-aabbccddeeff"), "slice.ToUuid() should preserve RFC 4122 ordering");

			// round-trip
			uuid = Uuid128.NewUuid();
			Assert.That(MutableSlice.FromUuid128(uuid).ToUuid128(), Is.EqualTo(uuid));

			Assert.That(MutableSlice.FromStringAscii(uuid.ToString()).ToUuid128(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => MutableSlice.FromStringAscii("random text").ToUuid128(), Throws.InstanceOf<FormatException>());

			// should validate the arguments
			var x = MutableSlice.FromUuid128(uuid);
			Assert.That(() => MutateOffset(x, -1).ToUuid128(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 17).ToUuid128(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToUuid128(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_MutableSlice_FromUuid64()
		{
			// Verify that FoundationDb.Client.Uuid64 are stored as 64-bit UUIDs in big-endian

			// empty guid should be all zeroes
			MutableSlice slice = MutableSlice.FromUuid64(Uuid64.Empty);
			Assert.That(slice.ToHexaString(), Is.EqualTo("0000000000000000"));

			// UUIDs should be stored in lexicographical order
			var uuid = Uuid64.Parse("01234567-89abcdef");

			// byte order should follow the string!
			slice = MutableSlice.FromUuid64(uuid);
			Assert.That(slice.ToHexaString(), Is.EqualTo("0123456789ABCDEF"), "MutableSlice.FromUuid64() should preserve ordering");

			// ToByteArray() should also be safe
			slice = uuid.ToByteArray().AsMutableSlice();
			Assert.That(slice.ToHexaString(), Is.EqualTo("0123456789ABCDEF"));
		}

		[Test]
		public void Test_MutableSlice_ToUuid64()
		{
			// nil or empty should return Uuid64.Empty
			Uuid64 uuid = MutableSlice.Nil.ToUuid64();
			Assert.That(uuid, Is.EqualTo(Uuid64.Empty));
			uuid = MutableSlice.Empty.ToUuid64();
			Assert.That(uuid, Is.EqualTo(Uuid64.Empty));

			// all zeroes should also return Uuid64.Empty
			uuid = MutableSlice.Create(8).ToUuid64();
			Assert.That(uuid, Is.EqualTo(Uuid64.Empty));

			// hexadecimal text representation
			uuid = MutableSlice.FromHexa("0123456789abcdef").ToUuid64();
			Assert.That(uuid.ToInt64(), Is.EqualTo(0x123456789abcdef), "slice.ToUuid64() should preserve ordering");

			// round-trip
			uuid = Uuid64.NewUuid();
			Assert.That(MutableSlice.FromUuid64(uuid).ToUuid64(), Is.EqualTo(uuid));

			Assert.That(MutableSlice.FromStringAscii(uuid.ToString()).ToUuid64(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => MutableSlice.FromStringAscii("random text").ToUuid64(), Throws.InstanceOf<FormatException>());

			// should validate the arguments
			var x = MutableSlice.FromUuid64(uuid);
			Assert.That(() => MutateOffset(x, -1).ToUuid64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 9).ToUuid64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToUuid64(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		[Test]
		public void Test_MutableSlice_FromBase64()
		{
			// numl string is Nil slice
			MutableSlice slice = MutableSlice.FromBase64(default(string));
			Assert.That(slice, Is.EqualTo(MutableSlice.Nil));

			// empty string is empty slice
			slice = MutableSlice.FromBase64("");
			Assert.That(slice, Is.EqualTo(MutableSlice.Empty));

			// UUIDs should be stored in lexicographical order
			slice = MutableSlice.FromBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes("Hello, World!")));
			Assert.That(slice.ToUnicode(), Is.EqualTo("Hello, World!"));

			// malformed
			Assert.That(() => MutableSlice.FromBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes("Hello, World!")).Substring(1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutableSlice.FromBase64("This is not a base64 string!"), Throws.InstanceOf<FormatException>());
		}

		#region Equality / Comparison / HashCodes...

		[Test]
		[SuppressMessage("ReSharper", "EqualExpressionComparison")]
		public void Test_MutableSlice_Equality()
		{
#pragma warning disable 1718
			// a == b == c && x != y && a != x
			var a = new byte[] { 1, 2, 3 }.AsMutableSlice();
			var b = new byte[] { 1, 2, 3 }.AsMutableSlice();
			var c = new byte[] { 0, 1, 2, 3, 4 }.AsMutableSlice(1, 3);
			var x = new byte[] { 4, 5, 6 }.AsMutableSlice();
			var y = new byte[] { 1, 2, 3 }.AsMutableSlice(0, 2);
			var z = new byte[] { 1, 2, 3, 4 }.AsMutableSlice();

			// IEquatable<MutableSlice>
			// equals
			Assert.That(a, Is.EqualTo(a));
			Assert.That(a, Is.EqualTo(b));
			Assert.That(a, Is.EqualTo(c));
			Assert.That(b, Is.EqualTo(a));
			Assert.That(b, Is.EqualTo(b));
			Assert.That(b, Is.EqualTo(c));
			Assert.That(c, Is.EqualTo(a));
			Assert.That(c, Is.EqualTo(b));
			Assert.That(c, Is.EqualTo(c));
			// not equals
			Assert.That(a, Is.Not.EqualTo(x));
			Assert.That(a, Is.Not.EqualTo(y));
			Assert.That(a, Is.Not.EqualTo(z));

			// Default Comparer
			// equals
			Assert.That(MutableSlice.Comparer.Default.Equals(a, a), Is.True);
			Assert.That(MutableSlice.Comparer.Default.Equals(a, b), Is.True);
			Assert.That(MutableSlice.Comparer.Default.Equals(a, c), Is.True);
			Assert.That(MutableSlice.Comparer.Default.Equals(b, a), Is.True);
			Assert.That(MutableSlice.Comparer.Default.Equals(b, b), Is.True);
			Assert.That(MutableSlice.Comparer.Default.Equals(b, c), Is.True);
			Assert.That(MutableSlice.Comparer.Default.Equals(c, a), Is.True);
			Assert.That(MutableSlice.Comparer.Default.Equals(c, b), Is.True);
			Assert.That(MutableSlice.Comparer.Default.Equals(c, c), Is.True);
			// not equals
			Assert.That(MutableSlice.Comparer.Default.Equals(a, x), Is.False);
			Assert.That(MutableSlice.Comparer.Default.Equals(a, y), Is.False);
			Assert.That(MutableSlice.Comparer.Default.Equals(a, z), Is.False);

			// Operators
			// ==
			Assert.That(a == a, Is.True);
			Assert.That(a == b, Is.True);
			Assert.That(a == c, Is.True);
			Assert.That(b == a, Is.True);
			Assert.That(b == b, Is.True);
			Assert.That(b == c, Is.True);
			Assert.That(c == a, Is.True);
			Assert.That(c == b, Is.True);
			Assert.That(c == c, Is.True);
			Assert.That(a == x, Is.False);
			Assert.That(a == y, Is.False);
			Assert.That(a == z, Is.False);
			// !=
			Assert.That(a != a, Is.False);
			Assert.That(a != b, Is.False);
			Assert.That(a != c, Is.False);
			Assert.That(b != a, Is.False);
			Assert.That(b != b, Is.False);
			Assert.That(b != c, Is.False);
			Assert.That(c != a, Is.False);
			Assert.That(c != b, Is.False);
			Assert.That(c != c, Is.False);
			Assert.That(a != x, Is.True);
			Assert.That(a != y, Is.True);
			Assert.That(a != z, Is.True);
#pragma warning restore 1718

		}

		[Test]
		public void Test_MutableSlice_Equals_Slice()
		{

			var a = new byte[] { 1, 2, 3 }.AsSlice();
			var b = new byte[] { 1, 2, 3 }.AsSlice();
			var c = new byte[] { 0, 1, 2, 3, 4 }.AsSlice(1, 3);
			var x = new byte[] { 4, 5, 6 }.AsSlice();
			var y = new byte[] { 1, 2, 3 }.AsSlice(0, 2);
			var z = new byte[] { 1, 2, 3, 4 }.AsSlice();

			// equals
			Assert.That(a.Equals(a), Is.True);
			Assert.That(a.Equals(b), Is.True);
			Assert.That(a.Equals(c), Is.True);
			Assert.That(b.Equals(a), Is.True);
			Assert.That(b.Equals(b), Is.True);
			Assert.That(b.Equals(c), Is.True);
			Assert.That(c.Equals(a), Is.True);
			Assert.That(c.Equals(b), Is.True);
			Assert.That(c.Equals(c), Is.True);
			Assert.That(MutableSlice.Nil.Equals(MutableSlice.Nil), Is.True);
			Assert.That(MutableSlice.Empty.Equals(MutableSlice.Empty), Is.True);

			// not equals
			Assert.That(a.Equals(x), Is.False);
			Assert.That(a.Equals(y), Is.False);
			Assert.That(a.Equals(z), Is.False);
			Assert.That(a.Equals(MutableSlice.Nil), Is.False);
			Assert.That(a.Equals(MutableSlice.Empty), Is.False);
			Assert.That(MutableSlice.Empty.Equals(MutableSlice.Nil), Is.False);
			Assert.That(MutableSlice.Nil.Equals(MutableSlice.Empty), Is.False);
		}

		[Test]
		public void Test_MutableSlice_Equality_Corner_Cases()
		{
			Assert.That(default(byte[]).AsSlice(), Is.EqualTo(MutableSlice.Nil));
			Assert.That(new byte[0].AsSlice(), Is.EqualTo(MutableSlice.Empty));

			Assert.That(default(byte[]).AsSlice() == MutableSlice.Nil, Is.True, "null == Nil");
			Assert.That(default(byte[]).AsSlice() == MutableSlice.Empty, Is.False, "null != Empty");
			Assert.That(new byte[0].AsSlice() == MutableSlice.Empty, Is.True, "[0] == Empty");
			Assert.That(new byte[0].AsSlice() == MutableSlice.Nil, Is.False, "[0] != Nill");

			// "slice == null" should be the equivalent to "slice.IsNull" so only true for MutableSlice.Nil
			Assert.That(MutableSlice.Nil == null, Is.True, "'MutableSlice.Nil == null' is true");
			Assert.That(MutableSlice.Empty == null, Is.False, "'MutableSlice.Empty == null' is false");
			Assert.That(MutableSlice.FromByte(1) == null, Is.False, "'[1] == null' is false");
			Assert.That(null == MutableSlice.Nil, Is.True, "'MutableSlice.Nil == null' is true");
			Assert.That(null == MutableSlice.Empty, Is.False, "'MutableSlice.Empty == null' is false");
			Assert.That(null == MutableSlice.FromByte(1), Is.False, "'[1] == null' is false");

			// "slice != null" should be the equivalent to "slice.HasValue" so only false for MutableSlice.Nil
			Assert.That(MutableSlice.Nil != null, Is.False, "'MutableSlice.Nil != null' is false");
			Assert.That(MutableSlice.Empty != null, Is.True, "'MutableSlice.Empty != null' is true");
			Assert.That(MutableSlice.FromByte(1) != null, Is.True, "'[1] != null' is true");
			Assert.That(null != MutableSlice.Nil, Is.False, "'MutableSlice.Nil != null' is false");
			Assert.That(null != MutableSlice.Empty, Is.True, "'MutableSlice.Empty != null' is true");
			Assert.That(null != MutableSlice.FromByte(1), Is.True, "'[1] != null' is true");
		}

		[Test]
		public void Test_MutableSlice_Equality_TwoByteArrayWithSameContentShouldReturnTrue()
		{
			var s1 = MutableSlice.FromStringAscii("abcd");
			var s2 = MutableSlice.FromStringAscii("abcd");
			Assert.That(s1.Equals(s2), Is.True, "'abcd' should equals 'abcd'");
		}

		[Test]
		public void Test_MutableSlice_Equality_TwoByteArrayWithSameContentFromSameOriginalBufferShouldReturnTrue()
		{
			var origin = System.Text.Encoding.ASCII.GetBytes("abcdabcd");
			var a1 = new ArraySegment<byte>(origin, 0, 4); //"abcd", refer first part of origin buffer
			var s1 = a1.AsSlice(); //
			var a2 = new ArraySegment<byte>(origin, 4, 4);//"abcd", refer second part of origin buffer
			var s2 = a2.AsSlice();
			Assert.That(s1.Equals(s2), Is.True, "'abcd' should equals 'abcd'");
		}

		[Test]
		public void Test_MutableSlice_Equality_Malformed()
		{
			var good = MutableSlice.FromStringAscii("good");
			var evil = MutableSlice.FromStringAscii("evil");

			// argument should be validated
			Assert.That(() => good.Equals(MutateOffset(evil, -1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateCount(evil, 666)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateArray(evil, null)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateOffset(MutateCount(evil, 5), -1)), Throws.InstanceOf<FormatException>());

			// instance should also be validated
			Assert.That(() => MutateOffset(evil, -1).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(evil, 666).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(evil, null).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateOffset(MutateCount(evil, 5), -1).Equals(good), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_MutableSlice_Hash_Code()
		{
			// note: the test values MAY change if the hashcode algorithm is modified.
			// That means that if all the asserts in this test fail, you should probably ensure that the expected results are still valid.

			Assert.That(MutableSlice.Nil.GetHashCode(), Is.EqualTo(0), "Nil hashcode should always be 0");
			Assert.That(MutableSlice.Empty.GetHashCode(), Is.Not.EqualTo(0), "Empty hashcode should not be equal to 0");

			Assert.That(MutableSlice.FromString("abc").GetHashCode(), Is.EqualTo(MutableSlice.FromString("abc").GetHashCode()), "Hashcode should not depend on the backing array");
			Assert.That(MutableSlice.FromString("zabcz").Substring(1, 3).GetHashCode(), Is.EqualTo(MutableSlice.FromString("abc").GetHashCode()), "Hashcode should not depend on the offset in the array");
			Assert.That(MutableSlice.FromString("abc").GetHashCode(), Is.Not.EqualTo(MutableSlice.FromString("abcd").GetHashCode()), "Hashcode should include all the bytes");

			Assert.That(MutableSlice.Comparer.Default.GetHashCode(MutableSlice.Nil), Is.EqualTo(0), "Nil hashcode should always be 0");
			Assert.That(MutableSlice.Comparer.Default.GetHashCode(MutableSlice.Empty), Is.Not.EqualTo(0), "Empty hashcode should not be equal to 0");
			Assert.That(MutableSlice.Comparer.Default.GetHashCode(MutableSlice.FromString("abc")), Is.EqualTo(MutableSlice.FromString("abc").GetHashCode()), "Hashcode should not depend on the backing array");
			Assert.That(MutableSlice.Comparer.Default.GetHashCode(MutableSlice.FromString("zabcz").Substring(1, 3)), Is.EqualTo(MutableSlice.FromString("abc").GetHashCode()), "Hashcode should not depend on the offset in the array");
			Assert.That(MutableSlice.Comparer.Default.GetHashCode(MutableSlice.FromString("abc")), Is.Not.EqualTo(MutableSlice.FromString("abcd").GetHashCode()), "Hashcode should include all the bytes");

			// should validate the arguments
			var x = MutableSlice.FromString("evil");
			Assert.That(() => MutateOffset(x, -1).GetHashCode(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 17).GetHashCode(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).GetHashCode(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		[SuppressMessage("ReSharper", "EqualExpressionComparison")]
		public void Test_MutableSlice_Comparison()
		{
#pragma warning disable 1718
			var a = MutableSlice.FromStringAscii("a");
			var ab = MutableSlice.FromStringAscii("ab");
			var abc = MutableSlice.FromStringAscii("abc");
			var abc2 = MutableSlice.FromStringAscii("abc"); // same bytes but different buffer
			var b = MutableSlice.FromStringAscii("b");

			// CompateTo
			// a = b
			Assert.That(a.CompareTo(a), Is.EqualTo(0));
			Assert.That(ab.CompareTo(ab), Is.EqualTo(0));
			Assert.That(abc.CompareTo(abc), Is.EqualTo(0));
			Assert.That(abc.CompareTo(abc2), Is.EqualTo(0));
			// a < b
			Assert.That(a.CompareTo(b), Is.LessThan(0));
			Assert.That(a.CompareTo(ab), Is.LessThan(0));
			Assert.That(a.CompareTo(abc), Is.LessThan(0));
			// a > b
			Assert.That(b.CompareTo(a), Is.GreaterThan(0));
			Assert.That(b.CompareTo(ab), Is.GreaterThan(0));
			Assert.That(b.CompareTo(abc), Is.GreaterThan(0));

			// Default Comparer
			// a = b
			Assert.That(MutableSlice.Comparer.Default.Compare(a, a), Is.EqualTo(0));
			Assert.That(MutableSlice.Comparer.Default.Compare(ab, ab), Is.EqualTo(0));
			Assert.That(MutableSlice.Comparer.Default.Compare(abc, abc), Is.EqualTo(0));
			Assert.That(MutableSlice.Comparer.Default.Compare(abc, abc2), Is.EqualTo(0));
			// a < b
			Assert.That(MutableSlice.Comparer.Default.Compare(a, b), Is.LessThan(0));
			Assert.That(MutableSlice.Comparer.Default.Compare(a, ab), Is.LessThan(0));
			Assert.That(MutableSlice.Comparer.Default.Compare(a, abc), Is.LessThan(0));
			// a > b
			Assert.That(MutableSlice.Comparer.Default.Compare(b, a), Is.GreaterThan(0));
			Assert.That(MutableSlice.Comparer.Default.Compare(b, ab), Is.GreaterThan(0));
			Assert.That(MutableSlice.Comparer.Default.Compare(b, abc), Is.GreaterThan(0));

			// Operators
			// <
			Assert.That(a < a, Is.False);
			Assert.That(a < ab, Is.True);
			Assert.That(ab < b, Is.True);
			Assert.That(a < b, Is.True);
			Assert.That(ab < a, Is.False);
			Assert.That(b < ab, Is.False);
			Assert.That(b < a, Is.False);
			Assert.That(abc < abc2, Is.False);
			// <=
			Assert.That(a <= a, Is.True);
			Assert.That(a <= ab, Is.True);
			Assert.That(ab <= b, Is.True);
			Assert.That(a <= b, Is.True);
			Assert.That(ab <= a, Is.False);
			Assert.That(b <= ab, Is.False);
			Assert.That(b <= a, Is.False);
			Assert.That(abc <= abc2, Is.True);
			// >
			Assert.That(a > a, Is.False);
			Assert.That(ab > a, Is.True);
			Assert.That(b > ab, Is.True);
			Assert.That(b > a, Is.True);
			Assert.That(a > ab, Is.False);
			Assert.That(ab > b, Is.False);
			Assert.That(a > b, Is.False);
			Assert.That(abc > abc2, Is.False);
			// >=
			Assert.That(a >= a, Is.True);
			Assert.That(ab >= a, Is.True);
			Assert.That(b >= ab, Is.True);
			Assert.That(b >= a, Is.True);
			Assert.That(a >= ab, Is.False);
			Assert.That(ab >= b, Is.False);
			Assert.That(a >= b, Is.False);
			Assert.That(abc >= abc2, Is.True);
#pragma warning restore 1718
		}

		[Test]
		public void Test_MutableSlice_Comparison_Corner_Cases()
		{
			// Nil == Empty
			Assert.That(MutableSlice.Nil.CompareTo(MutableSlice.Nil), Is.EqualTo(0));
			Assert.That(MutableSlice.Empty.CompareTo(MutableSlice.Empty), Is.EqualTo(0));
			Assert.That(MutableSlice.Nil.CompareTo(MutableSlice.Empty), Is.EqualTo(0));
			Assert.That(MutableSlice.Empty.CompareTo(MutableSlice.Nil), Is.EqualTo(0));

			// X > NULL, NULL < X
			var abc = MutableSlice.FromStringAscii("abc");
			Assert.That(abc.CompareTo(MutableSlice.Nil), Is.GreaterThan(0));
			Assert.That(abc.CompareTo(MutableSlice.Empty), Is.GreaterThan(0));
			Assert.That(MutableSlice.Nil.CompareTo(abc), Is.LessThan(0));
			Assert.That(MutableSlice.Empty.CompareTo(abc), Is.LessThan(0));
		}

		[Test]
		public void Test_MutableSlice_Comparison_Malformed()
		{
			var good = MutableSlice.FromStringAscii("good");
			var evil = MutableSlice.FromStringAscii("evil");

			// argument should be validated
			Assert.That(() => good.CompareTo(MutateOffset(evil, -1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateCount(evil, 666)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateArray(evil, null)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateOffset(MutateCount(evil, 5), -1)), Throws.InstanceOf<FormatException>());

			// instance should also be validated
			Assert.That(() => MutateOffset(evil, -1).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(evil, 666).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(evil, null).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateOffset(MutateCount(evil, 5), -1).CompareTo(good), Throws.InstanceOf<FormatException>());
		}

		#endregion

		private static readonly string UNICODE_TEXT = "Thïs Ïs à strîng thât contaÎns somé ùnicodè charactêrs and should be encoded in UTF-8: よろしくお願いします";
		private static readonly byte[] UNICODE_BYTES = Encoding.UTF8.GetBytes(UNICODE_TEXT);

		[Test]
		public void Test_MutableSlice_FromStream()
		{
			MutableSlice slice;

			using(var ms = new MemoryStream(UNICODE_BYTES))
			{
				slice = MutableSlice.FromStream(ms);
			}
			Assert.That(slice.Count, Is.EqualTo(UNICODE_BYTES.Length));
			Assert.That(slice.GetBytes(), Is.EqualTo(UNICODE_BYTES));
			Assert.That(slice.ToUnicode(), Is.EqualTo(UNICODE_TEXT));

			// ReSharper disable once AssignNullToNotNullAttribute
			Assert.That(() => MutableSlice.FromStream(null), Throws.ArgumentNullException, "Should throw if null");
			Assert.That(MutableSlice.FromStream(Stream.Null), Is.EqualTo(MutableSlice.Nil), "Stream.Null should return MutableSlice.Nil");

			using(var ms = new MemoryStream())
			{
				ms.Close();
				Assert.That(() => MutableSlice.FromStream(ms), Throws.InstanceOf<InvalidOperationException>(), "Reading from a disposed stream should throw");
			}
		}

		[Test]
		public void Test_MutableSlice_Substring()
		{
			Assert.That(MutableSlice.Empty.Substring(0), Is.EqualTo(MutableSlice.Empty));
			Assert.That(MutableSlice.Empty.Substring(0, 0), Is.EqualTo(MutableSlice.Empty));
			Assert.That(() => MutableSlice.Empty.Substring(0, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutableSlice.Empty.Substring(1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutableSlice.Empty.Substring(1, 0), Throws.Nothing, "We allow out of bound substring if count == 0");

			// Substring(offset)
			Assert.That(Value("Hello, World!").Substring(0), Is.EqualTo(Value("Hello, World!")));
			Assert.That(Value("Hello, World!").Substring(7), Is.EqualTo(Value("World!")));
			Assert.That(Value("Hello, World!").Substring(12), Is.EqualTo(Value("!")));
			Assert.That(Value("Hello, World!").Substring(13), Is.EqualTo(MutableSlice.Empty));
			Assert.That(() => Value("Hello, World!").Substring(14), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// Substring(offset, count)
			Assert.That(Value("Hello, World!").Substring(0, 5), Is.EqualTo(Value("Hello")));
			Assert.That(Value("Hello, World!").Substring(7, 5), Is.EqualTo(Value("World")));
			Assert.That(Value("Hello, World!").Substring(7, 6), Is.EqualTo(Value("World!")));
			Assert.That(Value("Hello, World!").Substring(12, 1), Is.EqualTo(Value("!")));
			Assert.That(Value("Hello, World!").Substring(13, 0), Is.EqualTo(MutableSlice.Empty));
			Assert.That(() => Value("Hello, World!").Substring(7, 7), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Value("Hello, World!").Substring(13, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Value("Hello, World!").Substring(7, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// Substring(offset) negative indexing
			Assert.That(Value("Hello, World!").Substring(-1), Is.EqualTo(Value("!")));
			Assert.That(Value("Hello, World!").Substring(-2), Is.EqualTo(Value("d!")));
			Assert.That(Value("Hello, World!").Substring(-6), Is.EqualTo(Value("World!")));
			Assert.That(Value("Hello, World!").Substring(-13), Is.EqualTo(Value("Hello, World!")));
			Assert.That(() => Value("Hello, World!").Substring(-14), Throws.InstanceOf<ArgumentOutOfRangeException>());

		}

		#region Black Magic Incantations...

		// The MutableSlice struct is not blittable, so we can't take its address and modify it via pointers trickery.
		// Since its ctor is checking the arguments in Debug mode and all its fields are readonly, the only way to inject bad values is to use reflection.

		private static MutableSlice MutateOffset(MutableSlice value, int offset)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(MutableSlice).GetField("Offset").SetValue(tmp, offset);
			return (MutableSlice) tmp;
		}

		private static MutableSlice MutateCount(MutableSlice value, int offset)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(MutableSlice).GetField("Offset").SetValue(tmp, offset);
			return (MutableSlice) tmp;
		}

		private static MutableSlice MutateArray(MutableSlice value, byte[] array)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(MutableSlice).GetField("Array").SetValue(tmp, array);
			return (MutableSlice) tmp;
		}

		#endregion

		public new static MutableSlice Value(string text)
		{
			return MutableSlice.FromString(text);
		}

	}

}
