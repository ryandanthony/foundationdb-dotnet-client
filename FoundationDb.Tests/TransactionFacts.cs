﻿#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client;
using FoundationDb.Client.Tuples;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class TransactionFacts
	{

		[TestFixtureSetUp]
		public void Setup()
		{
			//TODO: cleanup ?
		}

		[TestFixtureTearDown]
		public void Teardown()
		{
			Fdb.Stop();
		}

		[Test]
		public async Task Test_Can_Create_Transaction()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");
					Assert.That(tr.Database, Is.SameAs(db), "Transaction should reference the parent Database");
				}
			}
		}
		
		[Test]
		public async Task Test_Creating_Concurrent_Transactions_Are_Independent()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				FdbTransaction tr1 = null;
				FdbTransaction tr2 = null;
				try
				{
					tr1 = db.BeginTransaction();
					tr2 = db.BeginTransaction();

					Assert.That(tr1, Is.Not.Null);
					Assert.That(tr2, Is.Not.Null);
					Assert.That(tr1, Is.Not.SameAs(tr2), "Should create two different transaction objects");
				}
				finally
				{
					if (tr1 != null) tr1.Dispose();
					if (tr2 != null) tr2.Dispose();

				}
			}
		}

		[Test]
		public async Task Test_Commiting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!
				}
			}
		}

		[Test]
		public async Task Test_Resetting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!

					// Committed version should be -1 (where is is specified?)
					long ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Committed version of empty transaction should be -1");

				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Transaction_Read_Version()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					long ver = await tr.GetReadVersionAsync();
					Assert.That(ver, Is.GreaterThan(0), "Read version should be > 0");

					// if we ask for it again, we should have the same value
					long ver2 = await tr.GetReadVersionAsync();
					Assert.That(ver2, Is.EqualTo(ver), "Read version should not change inside same transaction");
				}
			}
		}

		[Test]
		public async Task Test_Write_And_Read_Simple_Keys()
		{
			// test that we can read and write simple keys

			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				long ticks = DateTime.UtcNow.Ticks;
				long writeVersion;
				long readVersion;

				// write a bunch of keys
				using (var tr = db.BeginTransaction())
				{
					tr.Set(FdbKey.Ascii("test.hello"), Slice.FromString("World!"));
					tr.Set(FdbKey.Ascii("test.timestamp"), Slice.FromInt64(ticks));
					tr.Set(FdbKey.Ascii("test.blob"), Slice.Create(new byte[] { 42, 123, 7 }));

					await tr.CommitAsync();

					writeVersion = tr.GetCommittedVersion();
					Assert.That(writeVersion, Is.GreaterThan(0), "Commited version of non-empty transaction should be > 0");
				}

				// read them back
				using (var tr = db.BeginTransaction())
				{
					Slice bytes;

					readVersion = await tr.GetReadVersionAsync();
					Assert.That(readVersion, Is.GreaterThan(0), "Read version should be > 0");

					bytes = await tr.GetAsync(FdbKey.Ascii("test.hello")); // => 1007 "past_version"
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count), Is.EqualTo("World!"));

					bytes = await tr.GetAsync(FdbKey.Ascii("test.timestamp"));
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(bytes.ToInt64(), Is.EqualTo(ticks));

					bytes = await tr.GetAsync(FdbKey.Ascii("test.blob"));
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(bytes.Array, Is.EqualTo(new byte[] { 42, 123, 7 }));
				}

				Assert.That(readVersion, Is.GreaterThanOrEqualTo(writeVersion), "Read version should not be before previous committed version");
			}
		}

		[Test]
		public async Task Test_Can_Set_Read_Version()
		{
			// Verify that we can set a read version on a transaction
			// * tr1 will set value to 1
			// * tr2 will set value to 2
			// * tr3 will SetReadVersion(TR1.CommittedVersion) and we expect it to read 1 (and not 2)

			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{

				long commitedVersion;

				// create first version
				using (var tr1 = db.BeginTransaction())
				{
					tr1.Set(FdbKey.Ascii("test.concurrent"), Slice.Create(new byte[] { 1 }));
					await tr1.CommitAsync();

					// get this version
					commitedVersion = tr1.GetCommittedVersion();
				}

				// mutate in another transaction
				using (var tr2 = db.BeginTransaction())
				{
					tr2.Set(FdbKey.Ascii("test.concurrent"), Slice.Create(new byte[] { 2 }));
					await tr2.CommitAsync();
				}

				// read the value with TR1's commited version
				using (var tr3 = db.BeginTransaction())
				{
					tr3.SetReadVersion(commitedVersion);

					long ver = await tr3.GetReadVersionAsync();
					Assert.That(ver, Is.EqualTo(commitedVersion), "GetReadVersion should return the same value as SetReadVersion!");

					var bytes = await tr3.GetAsync(FdbKey.Ascii("test.concurrent"));

					Assert.That(bytes, Is.Not.Null);
					Assert.That(bytes, Is.EqualTo(new byte[] { 1 }), "Should have seen the first version!");

				}

			}

		}

		[Test]
		public async Task Test_Can_Get_Range()
		{
			// test that we can get a range of keys

			const int N = 1000; // total item count

			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				// put test values in a namespace
				var tuple = FdbTuple.Create("GetRangeTest");
				// cleanup everything
				using (var tr = db.BeginTransaction())
				{
					tr.ClearRange(tuple);
					await tr.CommitAsync();
				}

				// insert all values (batched)
				Console.WriteLine("Inserting " + N.ToString("N0") + " keys...");
				var insert = Stopwatch.StartNew();

				using (var tr = db.BeginTransaction())
				{ 
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(tuple.Append(i), Slice.FromInt32(i));
					}

					await tr.CommitAsync();
				}
				insert.Stop();

				Console.WriteLine("Committed " + N + " keys in " + insert.Elapsed.TotalMilliseconds.ToString("N1") + " ms");

				// GetRange values

				using (var tr = db.BeginTransaction())
				{
					var res = tr.GetRange(tuple.Append(0), tuple.Append(N));
					Assert.That(res, Is.Not.Null);

					Console.WriteLine("Getting range " + res.Query.Begin + " -> " + res.Query.End + " ...");

					var range = Stopwatch.StartNew();
					var items = await res.ReadAllAsync();
					range.Stop();
					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));
					Console.WriteLine("Took " + range.Elapsed.TotalMilliseconds.ToString("N1") + " ms to get " + items.Count.ToString("N0") + " results");

					for (int i = 0; i < N; i++)
					{
						var kvp = items[i];

						// key should be a tuple in the correct order
						var key = FdbTuple.Unpack(kvp.Key);

						if (i % 128 == 0) Console.WriteLine(key.ToString() + " = " + kvp.Value.ToString());

						Assert.That(key.Count, Is.EqualTo(2));
						Assert.That(key.GetInt32(-1), Is.EqualTo(i));

						// value should be a guid
						Assert.That(kvp.Value.ToInt32(), Is.EqualTo(i));
					}
				}

			}
		}

		[Test]
		public async Task Test_Has_Access_To_System_Keys()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{

				using (var tr = db.BeginTransaction())
				{

					// should fail if access to system keys has not been requested

					try
					{
						var _ = await tr.GetRangeStartsWith(Slice.FromAscii("\xFF"), limit: 10).ReadAllAsync();
						Assert.Fail("Should not have access to system keys by default");
					}
					catch (Exception e)
					{
						Assert.That(e, Is.InstanceOf<FdbException>());
						var x = (FdbException)e;
						Assert.That(x.Code, Is.EqualTo(FdbError.KeyOutsideLegalRange));
					}

					// should succeed once system access has been requested
					tr.WithAccessToSystemKeys();

					var keys = await tr.GetRangeStartsWith(Slice.FromAscii("\xFF"), limit: 10).ReadAllAsync();
					Assert.That(keys, Is.Not.Null);
				}

			}
		}

		[Test]
		public async Task Test_Cannot_Read_Or_Write_Outside_Of_Restricted_Key_Space()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				var space = FdbTuple.Create(123);

				db.RestrictKeySpace(space);

				using (var tr = db.BeginTransaction())
				{
					// should allow writing inside the space
					tr.Set(space.Append("hello"), Slice.Empty);

					// should not allow outside of the space
					Assert.That(
						Assert.Throws<FdbException>(() => tr.Set(FdbTuple.Create(122), Slice.Empty), "Key is less than minimum allowed"),
						Has.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange)
					);
					Assert.That(
						Assert.Throws<FdbException>(() => tr.Set(FdbTuple.Create(123), Slice.Empty), "Key is more than maximum allowed"),
						Has.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange)
					);

					// should not allow the prefix itself
					Assert.That(
						Assert.Throws<FdbException>(() => tr.Set(space, Slice.Empty)),
						Has.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange)
					);
				}
			}
		}
	}
}
