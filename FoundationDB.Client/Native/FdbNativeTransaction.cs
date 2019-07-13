﻿#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

// enable this to help debug Transactions
//#define DEBUG_TRANSACTIONS
// enable this to capture the stacktrace of the ctor, when troubleshooting leaked transaction handles
//#define CAPTURE_STACKTRACES

namespace FoundationDB.Client.Native
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client.Core;
	using JetBrains.Annotations;

	/// <summary>Wraps a native FDB_TRANSACTION handle</summary>
	[DebuggerDisplay("Handle={m_handle}, Size={m_payloadBytes}, Closed={m_handle.IsClosed}")]
	internal class FdbNativeTransaction : IFdbTransactionHandler
	{
		private readonly FdbNativeDatabase m_database;
		/// <summary>FDB_TRANSACTION* handle</summary>
		private readonly TransactionHandle m_handle;
		/// <summary>Estimated current size of the transaction</summary>
		private int m_payloadBytes;

#if CAPTURE_STACKTRACES
		private StackTrace m_stackTrace;
#endif

		public FdbNativeTransaction([NotNull] FdbNativeDatabase db, [NotNull] TransactionHandle handle)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handle, nameof(handle));

			m_database = db;
			m_handle = handle;
#if CAPTURE_STACKTRACES
			m_stackTrace = new StackTrace();
#endif
		}

		//REVIEW: do we really need a destructor ? The handle is a SafeHandle, and will take care of itself...
		~FdbNativeTransaction()
		{
#if CAPTURE_STACKTRACES
			Trace.WriteLine("A transaction handle (" + m_handle + ", " + m_payloadBytes + " bytes written) was leaked by " + m_stackTrace);
#endif
#if DEBUG
			// If you break here, that means that a native transaction handler was leaked by a FdbTransaction instance (or that the transaction instance was leaked)
			if (Debugger.IsAttached) Debugger.Break();
#endif
			Dispose(false);
		}

		#region Properties...

		public bool IsClosed => m_handle.IsClosed;

		/// <summary>Native FDB_TRANSACTION* handle</summary>
		public TransactionHandle Handle => m_handle;

		/// <summary>Database handler that owns this transaction</summary>
		public FdbNativeDatabase Database => m_database;

		/// <summary>Estimated size of the transaction payload (in bytes)</summary>
		public int Size => m_payloadBytes;

		#endregion

		#region Options...

		public void SetOption(FdbTransactionOption option, Slice data)
		{
			Fdb.EnsureNotOnNetworkThread();

			unsafe
			{
				fixed (byte* ptr = data)
				{
					Fdb.DieOnError(FdbNative.TransactionSetOption(m_handle, option, ptr, data.Count));
				}
			}
		}

		#endregion

		#region Reading...

		public Task<long> GetReadVersionAsync(CancellationToken ct)
		{
			var future = FdbNative.TransactionGetReadVersion(m_handle);
			return FdbFuture.CreateTaskFromHandle(future,
				(h) =>
				{
					var err = FdbNative.FutureGetVersion(h, out long version);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetReadVersion() => err=" + err + ", version=" + version);
#endif
					Fdb.DieOnError(err);
					return version;
				},
				ct
			);
		}

		public void SetReadVersion(long version)
		{
			FdbNative.TransactionSetReadVersion(m_handle, version);
		}

		private static bool TryGetValueResult(FutureHandle h, out Slice result)
		{
			Contract.Requires(h != null);

			var err = FdbNative.FutureGetValue(h, out bool present, out result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].TryGetValueResult() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			Fdb.DieOnError(err);
			return present;
		}

		private static Slice GetValueResultBytes(FutureHandle h)
		{
			Contract.Requires(h != null);

			if (!TryGetValueResult(h, out Slice result))
			{
				return Slice.Nil;
			}
			return result;
		}

		public Task<Slice> GetAsync(in ReadOnlySpan<byte> key, bool snapshot, CancellationToken ct)
		{
			var future = FdbNative.TransactionGet(m_handle, key, snapshot);
			return FdbFuture.CreateTaskFromHandle(future, (h) => GetValueResultBytes(h), ct);
		}

		public Task<Slice[]> GetValuesAsync(Slice[] keys, bool snapshot, CancellationToken ct)
		{
			Contract.Requires(keys != null);

			if (keys.Length == 0) return Task.FromResult(Array.Empty<Slice>());

			var futures = new FutureHandle[keys.Length];
			try
			{
				for (int i = 0; i < keys.Length; i++)
				{
					futures[i] = FdbNative.TransactionGet(m_handle, keys[i], snapshot);
				}
			}
			catch
			{
				for (int i = 0; i < keys.Length; i++)
				{
					if (futures[i] == null) break;
					futures[i].Dispose();
				}
				throw;
			}
			return FdbFuture.CreateTaskFromHandleArray(futures, (h) => GetValueResultBytes(h), ct);
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		[NotNull]
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResult(FutureHandle h, out bool more, out Slice first, out Slice last)
		{
			var err = FdbNative.FutureGetKeyValueArray(h, out var result, out more);
			Fdb.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Ensures(result != null);
			first = result.Length > 0 ? result[0].Key : default;
			last = result.Length > 0 ? result[result.Length - 1].Key : default;
			return result;
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		[NotNull]
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResultKeysOnly(FutureHandle h, out bool more, out Slice first, out Slice last)
		{
			var err = FdbNative.FutureGetKeyValueArrayKeysOnly(h, out var result, out more);
			Fdb.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Ensures(result != null);
			first = result.Length > 0 ? result[0].Key : default;
			last = result.Length > 0 ? result[result.Length - 1].Key : default;
			return result;
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		[NotNull]
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResultValuesOnly(FutureHandle h, out bool more, out Slice first, out Slice last)
		{
			var err = FdbNative.FutureGetKeyValueArrayValuesOnly(h, out var result, out more, out first, out last);
			Fdb.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Ensures(result != null);
			return result;
		}

		/// <summary>Asynchronously fetch a new page of results</summary>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		public Task<FdbRangeChunk> GetRangeAsync(KeySelector begin, KeySelector end, FdbRangeOptions options, int iteration, bool snapshot, CancellationToken ct)
		{
			Contract.Requires(options != null);

			bool reversed = options.Reverse ?? false;
			var future = FdbNative.TransactionGetRange(m_handle, begin, end, options.Limit ?? 0, options.TargetBytes ?? 0, options.Mode ?? FdbStreamingMode.Iterator, iteration, snapshot, reversed);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) =>
				{
					var mode = options.Read ?? FdbReadMode.Both;
					KeyValuePair<Slice, Slice>[] items;
					bool hasMore;
					Slice first, last;
					switch (mode)
					{
						case FdbReadMode.Both:
						{
							items = GetKeyValueArrayResult(h, out hasMore, out first, out last);
							break;
						}
						case FdbReadMode.Keys:
						{
							items = GetKeyValueArrayResultKeysOnly(h, out hasMore, out first, out last);
							break;
						}
						case FdbReadMode.Values:
						{
							items = GetKeyValueArrayResultValuesOnly(h, out hasMore, out first, out last);
							break;
						}
						default:
						{
							throw new InvalidOperationException();
						}
					}
					return new FdbRangeChunk(items, hasMore, iteration, reversed, mode, first, last);
				},
				ct
			);
		}

		private static Slice GetKeyResult(FutureHandle h)
		{
			Contract.Requires(h != null);

			var err = FdbNative.FutureGetKey(h, out Slice result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetKeyResult() => err=" + err + ", result=" + result.ToString());
#endif
			Fdb.DieOnError(err);
			return result;
		}

		public Task<Slice> GetKeyAsync(KeySelector selector, bool snapshot, CancellationToken ct)
		{
			var future = FdbNative.TransactionGetKey(m_handle, selector, snapshot);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) => GetKeyResult(h),
				ct
			);
		}

		public Task<Slice[]> GetKeysAsync(KeySelector[] selectors, bool snapshot, CancellationToken ct)
		{
			Contract.Requires(selectors != null);

			var futures = new FutureHandle[selectors.Length];
			try
			{
				for (int i = 0; i < selectors.Length; i++)
				{
					futures[i] = FdbNative.TransactionGetKey(m_handle, selectors[i], snapshot);
				}
			}
			catch
			{
				for (int i = 0; i < selectors.Length; i++)
				{
					if (futures[i] == null) break;
					futures[i].Dispose();
				}
				throw;
			}
			return FdbFuture.CreateTaskFromHandleArray(futures, (h) => GetKeyResult(h), ct);

		}

		#endregion

		#region Writing...

		public void Set(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> value)
		{
			FdbNative.TransactionSet(m_handle, key, value);

			// There is a 28-byte overhead pet Set(..) in a transaction
			// cf http://community.foundationdb.com/questions/547/transaction-size-limit
			Interlocked.Add(ref m_payloadBytes, key.Length + value.Length + 28);
		}

		public void Atomic(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> param, FdbMutationType type)
		{
			FdbNative.TransactionAtomicOperation(m_handle, key, param, type);

			//TODO: what is the overhead for atomic operations?
			Interlocked.Add(ref m_payloadBytes, key.Length + param.Length);

		}

		public void Clear(in ReadOnlySpan<byte> key)
		{
			FdbNative.TransactionClear(m_handle, key);
			// The key is converted to range [key, key.'\0'), and there is an overhead of 28-byte per operation
			Interlocked.Add(ref m_payloadBytes, (key.Length * 2) + 28 + 1);
		}

		public void ClearRange(in ReadOnlySpan<byte> beginKeyInclusive, in ReadOnlySpan<byte> endKeyExclusive)
		{
			FdbNative.TransactionClearRange(m_handle, beginKeyInclusive, endKeyExclusive);
			// There is an overhead of 28-byte per operation
			Interlocked.Add(ref m_payloadBytes, beginKeyInclusive.Length + endKeyExclusive.Length + 28);
		}

		public void AddConflictRange(in ReadOnlySpan<byte> beginKeyInclusive, in ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
		{
			FdbError err = FdbNative.TransactionAddConflictRange(m_handle, beginKeyInclusive, endKeyExclusive, type);
			Fdb.DieOnError(err);
		}

		[NotNull]
		private static string[] GetStringArrayResult(FutureHandle h)
		{
			Contract.Requires(h != null);

			var err = FdbNative.FutureGetStringArray(h, out string[] result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].FutureGetStringArray() => err=" + err + ", results=" + (result == null ? "<null>" : result.Length.ToString()));
#endif
			Fdb.DieOnError(err);
			Contract.Ensures(result != null); // can only be null in case of an errror
			return result;
		}

		public Task<string[]> GetAddressesForKeyAsync(in ReadOnlySpan<byte> key, CancellationToken ct)
		{
			var future = FdbNative.TransactionGetAddressesForKey(m_handle, key);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) => GetStringArrayResult(h),
				ct
			);
		}

		#endregion

		#region Watches...

		public FdbWatch Watch(Slice key, CancellationToken ct)
		{
			var future = FdbNative.TransactionWatch(m_handle, key);
			return new FdbWatch(
				FdbFuture.FromHandle<Slice>(future, (h) => key, ct),
				key
			);
		}

		#endregion

		#region State management...

		public long GetCommittedVersion()
		{
			var err = FdbNative.TransactionGetCommittedVersion(m_handle, out long version);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[" + m_id + "].GetCommittedVersion() => err=" + err + ", version=" + version);
#endif
			Fdb.DieOnError(err);
			return version;
		}

		public Task<VersionStamp> GetVersionStampAsync(CancellationToken ct)
		{
			var future = FdbNative.TransactionGetVersionStamp(m_handle);
			return FdbFuture.CreateTaskFromHandle<VersionStamp>(future, GetVersionStampResult, ct);
		}

		private static VersionStamp GetVersionStampResult(FutureHandle h)
		{
			Contract.Requires(h != null);
			var err = FdbNative.FutureGetVersionStamp(h, out VersionStamp stamp);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[" + m_id + "].FutureGetVersionStamp() => err=" + err + ", vs=" + stamp + ")");
#endif
			Fdb.DieOnError(err);

			return stamp;
		}


		/// <summary>
		/// Attempts to commit the sets and clears previously applied to the database snapshot represented by this transaction to the actual database. 
		/// The commit may or may not succeed – in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. 
		/// If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.
		/// </summary>
		/// <returns>Task that succeeds if the transaction was committed successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded. In these cases, CommitAsync() will throw CommitUnknownResult error. The OnErrorAsync() function treats this error as retryable, so retry loops that don’t check for CommitUnknownResult could execute the transaction twice. In these cases, you must consider the idempotence of the transaction.</remarks>
		public Task CommitAsync(CancellationToken ct)
		{
			var future = FdbNative.TransactionCommit(m_handle);
			return FdbFuture.CreateTaskFromHandle<object>(future, (h) => null, ct);
		}

		public Task OnErrorAsync(FdbError code, CancellationToken ct)
		{
			var future = FdbNative.TransactionOnError(m_handle, code);
			return FdbFuture.CreateTaskFromHandle<object>(future, (h) => { ResetInternal(); return null; }, ct);
		}

		public void Reset()
		{
			FdbNative.TransactionReset(m_handle);
			ResetInternal();
		}

		public void Cancel()
		{
			FdbNative.TransactionCancel(m_handle);
		}

		private void ResetInternal()
		{
			m_payloadBytes = 0;
		}

		#endregion

		#region IDisposable...

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Dispose of the handle
				if (!m_handle.IsClosed) m_handle.Dispose();
			}
		}

		#endregion

	}

}
