using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;





namespace Dapper
{
	public static class SqlMapper
	{
		public interface IDynamicParameters
		{
			void AddParameters(IDbCommand command);
		}

		private class Link<TKey, TValue> where TKey : class
		{
			public TKey Key
			{
				get;
				private set;
			}

			public TValue Value
			{
				get;
				private set;
			}

			public SqlMapper.Link<TKey, TValue> Tail
			{
				get;
				private set;
			}

			public static bool TryGet(SqlMapper.Link<TKey, TValue> link, TKey key, out TValue value)
			{
				while (link != null)
				{
					if (key == link.Key)
					{
						value = link.Value;
						return true;
					}
					link = link.Tail;
				}
				value = default(TValue);
				return false;
			}

			public static bool TryAdd(ref SqlMapper.Link<TKey, TValue> head, TKey key, ref TValue value)
			{
				TValue tValue;
				while (true)
				{
					SqlMapper.Link<TKey, TValue> link = Interlocked.CompareExchange<SqlMapper.Link<TKey, TValue>>(ref head, null, null);
					if (SqlMapper.Link<TKey, TValue>.TryGet(link, key, out tValue))
					{
						break;
					}
					SqlMapper.Link<TKey, TValue> value2 = new SqlMapper.Link<TKey, TValue>(key, value, link);
					if (Interlocked.CompareExchange<SqlMapper.Link<TKey, TValue>>(ref head, value2, link) == link)
					{
						return true;
					}
				}
				value = tValue;
				return false;
			}

			private Link(TKey key, TValue value, SqlMapper.Link<TKey, TValue> tail)
			{
				this.Key = key;
				this.Value = value;
				this.Tail = tail;
			}
		}

		private class CacheInfo
		{
			public object Deserializer
			{
				get;
				set;
			}

			public object[] OtherDeserializers
			{
				get;
				set;
			}

			public Action<IDbCommand, object> ParamReader
			{
				get;
				set;
			}
		}

		internal class Identity : IEquatable<SqlMapper.Identity>
		{
			private readonly string sql;

			private readonly int hashCode;

			private readonly int gridIndex;

			private readonly Type type;

			private readonly string connectionString;

			internal readonly Type parametersType;

			internal SqlMapper.Identity ForGrid(Type primaryType, int gridIndex)
			{
				return new SqlMapper.Identity(this.sql, this.connectionString, primaryType, this.parametersType, null, gridIndex);
			}

			internal Identity(string sql, IDbConnection connection, Type type, Type parametersType, Type[] otherTypes) : this(sql, connection.ConnectionString, type, parametersType, otherTypes, 0)
			{
			}

			private Identity(string sql, string connectionString, Type type, Type parametersType, Type[] otherTypes, int gridIndex)
			{
				this.sql = sql;
				this.connectionString = connectionString;
				this.type = type;
				this.parametersType = parametersType;
				this.gridIndex = gridIndex;
				this.hashCode = 17;
				this.hashCode = this.hashCode * 23 + gridIndex.GetHashCode();
				this.hashCode = this.hashCode * 23 + ((sql == null) ? 0 : sql.GetHashCode());
				this.hashCode = this.hashCode * 23 + ((type == null) ? 0 : type.GetHashCode());
				if (otherTypes != null)
				{
					for (int i = 0; i < otherTypes.Length; i++)
					{
						Type type2 = otherTypes[i];
						this.hashCode = this.hashCode * 23 + ((type2 == null) ? 0 : type2.GetHashCode());
					}
				}
				this.hashCode = this.hashCode * 23 + ((connectionString == null) ? 0 : connectionString.GetHashCode());
				this.hashCode = this.hashCode * 23 + ((parametersType == null) ? 0 : parametersType.GetHashCode());
			}

			public override bool Equals(object obj)
			{
				return this.Equals(obj as SqlMapper.Identity);
			}

			public override int GetHashCode()
			{
				return this.hashCode;
			}

			public bool Equals(SqlMapper.Identity other)
			{
				return other != null && this.gridIndex == other.gridIndex && this.type == other.type && this.sql == other.sql && this.connectionString == other.connectionString && this.parametersType == other.parametersType;
			}
		}

		private class DontMap
		{
		}

		private class FastExpando : DynamicObject, IDictionary<string, object>, ICollection<KeyValuePair<string, object>>, IEnumerable<KeyValuePair<string, object>>, IEnumerable
		{
			private IDictionary<string, object> data;

			ICollection<string> IDictionary<string, object>.Keys
			{
				get
				{
					return this.data.Keys;
				}
			}

			ICollection<object> IDictionary<string, object>.Values
			{
				get
				{
					return this.data.Values;
				}
			}

			object IDictionary<string, object>.this[string key]
			{
				get
				{
					return this.data[key];
				}
				set
				{
					throw new NotImplementedException();
				}
			}

			int ICollection<KeyValuePair<string, object>>.Count
			{
				get
				{
					return this.data.Count;
				}
			}

			bool ICollection<KeyValuePair<string, object>>.IsReadOnly
			{
				get
				{
					return true;
				}
			}

			public static SqlMapper.FastExpando Attach(IDictionary<string, object> data)
			{
				return new SqlMapper.FastExpando
				{
					data = data
				};
			}

			public override bool TrySetMember(SetMemberBinder binder, object value)
			{
				this.data[binder.Name] = value;
				return true;
			}

			public override bool TryGetMember(GetMemberBinder binder, out object result)
			{
				return this.data.TryGetValue(binder.Name, out result);
			}

			void IDictionary<string, object>.Add(string key, object value)
			{
				throw new NotImplementedException();
			}

			bool IDictionary<string, object>.ContainsKey(string key)
			{
				return this.data.ContainsKey(key);
			}

			bool IDictionary<string, object>.Remove(string key)
			{
				throw new NotImplementedException();
			}

			bool IDictionary<string, object>.TryGetValue(string key, out object value)
			{
				return this.data.TryGetValue(key, out value);
			}

			void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
			{
				throw new NotImplementedException();
			}

			void ICollection<KeyValuePair<string, object>>.Clear()
			{
				throw new NotImplementedException();
			}

			bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
			{
				return this.data.Contains(item);
			}

			void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
			{
				this.data.CopyTo(array, arrayIndex);
			}

			bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
			{
				throw new NotImplementedException();
			}

			IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
			{
				return this.data.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return this.data.GetEnumerator();
			}
		}

		public class GridReader : IDisposable
		{
			private IDataReader reader;

			private IDbCommand command;

			private SqlMapper.Identity identity;

			private int gridIndex;

			private bool consumed;

			internal GridReader(IDbCommand command, IDataReader reader, SqlMapper.Identity identity)
			{
				this.command = command;
				this.reader = reader;
				this.identity = identity;
			}

			public IEnumerable<T> Read<T>()
			{
				if (this.reader == null)
				{
					throw new ObjectDisposedException(base.GetType().Name);
				}
				if (this.consumed)
				{
					throw new InvalidOperationException("Each grid can only be iterated once");
				}
				SqlMapper.Identity typedIdentity = this.identity.ForGrid(typeof(T), this.gridIndex);
				SqlMapper.CacheInfo cacheInfo = SqlMapper.GetCacheInfo(typedIdentity);
				Func<IDataReader, T> func = (Func<IDataReader, T>)cacheInfo.Deserializer;
				if (func == null)
				{
					func = SqlMapper.GetDeserializer<T>(this.reader, 0, -1, false);
					cacheInfo.Deserializer = func;
				}
				this.consumed = true;
				return this.ReadDeferred<T>(this.gridIndex, func, typedIdentity);
			}

			private IEnumerable<T> ReadDeferred<T>(int index, Func<IDataReader, T> deserializer, SqlMapper.Identity typedIdentity)
			{
				bool flag = true;
				try
				{
					while (index == this.gridIndex && this.reader.Read())
					{
						flag = false;
						T t = deserializer(this.reader);
						flag = true;
						yield return t;
					}
				}
				finally
				{
					if (!flag)
					{
						SqlMapper.PurgeQueryCache(typedIdentity);
					}
					if (index == this.gridIndex)
					{
						this.NextResult();
					}
				}
				yield break;
			}

			private void NextResult()
			{
				if (this.reader.NextResult())
				{
					this.gridIndex++;
					this.consumed = false;
					return;
				}
				this.Dispose();
			}

			public void Dispose()
			{
				if (this.reader != null)
				{
					this.reader.Dispose();
					this.reader = null;
				}
				if (this.command != null)
				{
					this.command.Dispose();
					this.command = null;
				}
			}
		}

		private static SqlMapper.Link<Type, Action<IDbCommand, bool>> bindByNameCache;

		private static readonly ConcurrentDictionary<SqlMapper.Identity, SqlMapper.CacheInfo> _queryCache;

		private static readonly Dictionary<RuntimeTypeHandle, DbType> typeMap;

		private static readonly MethodInfo enumParse;

		private static readonly MethodInfo getItem;

		private static Action<IDbCommand, bool> GetBindByName(Type commandType)
		{
			if (commandType == null)
			{
				return null;
			}
			Action<IDbCommand, bool> result;
			if (SqlMapper.Link<Type, Action<IDbCommand, bool>>.TryGet(SqlMapper.bindByNameCache, commandType, out result))
			{
				return result;
			}
			PropertyInfo property = commandType.GetProperty("BindByName", BindingFlags.Instance | BindingFlags.Public);
			result = null;
			ParameterInfo[] indexParameters;
			MethodInfo setMethod;
			if (property != null && property.CanWrite && property.PropertyType == typeof(bool) && ((indexParameters = property.GetIndexParameters()) == null || indexParameters.Length == 0) && (setMethod = property.GetSetMethod()) != null)
			{
				DynamicMethod dynamicMethod = new DynamicMethod(commandType.Name + "_BindByName", null, new Type[]
				{
					typeof(IDbCommand),
					typeof(bool)
				});
				ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
				iLGenerator.Emit(OpCodes.Ldarg_0);
				iLGenerator.Emit(OpCodes.Castclass, commandType);
				iLGenerator.Emit(OpCodes.Ldarg_1);
				iLGenerator.EmitCall(OpCodes.Callvirt, setMethod, null);
				iLGenerator.Emit(OpCodes.Ret);
				result = (Action<IDbCommand, bool>)dynamicMethod.CreateDelegate(typeof(Action<IDbCommand, bool>));
			}
			SqlMapper.Link<Type, Action<IDbCommand, bool>>.TryAdd(ref SqlMapper.bindByNameCache, commandType, ref result);
			return result;
		}

		private static void SetQueryCache(SqlMapper.Identity key, SqlMapper.CacheInfo value)
		{
			SqlMapper._queryCache[key] = value;
		}

		private static bool TryGetQueryCache(SqlMapper.Identity key, out SqlMapper.CacheInfo value)
		{
			return SqlMapper._queryCache.TryGetValue(key, out value);
		}

		private static void PurgeQueryCache(SqlMapper.Identity key)
		{
			SqlMapper.CacheInfo cacheInfo;
			SqlMapper._queryCache.TryRemove(key, out cacheInfo);
		}

		static SqlMapper()
		{
			SqlMapper._queryCache = new ConcurrentDictionary<SqlMapper.Identity, SqlMapper.CacheInfo>();
			SqlMapper.enumParse = typeof(Enum).GetMethod("Parse", new Type[]
			{
				typeof(Type),
				typeof(string),
				typeof(bool)
			});
			SqlMapper.getItem = (from p in typeof(IDataRecord).GetProperties(BindingFlags.Instance | BindingFlags.Public)
			where p.GetIndexParameters().Any<ParameterInfo>() && p.GetIndexParameters()[0].ParameterType == typeof(int)
			select p.GetGetMethod()).First<MethodInfo>();
			SqlMapper.typeMap = new Dictionary<RuntimeTypeHandle, DbType>();
			SqlMapper.typeMap[typeof(byte).TypeHandle] = DbType.Byte;
			SqlMapper.typeMap[typeof(sbyte).TypeHandle] = DbType.SByte;
			SqlMapper.typeMap[typeof(short).TypeHandle] = DbType.Int16;
			SqlMapper.typeMap[typeof(ushort).TypeHandle] = DbType.UInt16;
			SqlMapper.typeMap[typeof(int).TypeHandle] = DbType.Int32;
			SqlMapper.typeMap[typeof(uint).TypeHandle] = DbType.UInt32;
			SqlMapper.typeMap[typeof(long).TypeHandle] = DbType.Int64;
			SqlMapper.typeMap[typeof(ulong).TypeHandle] = DbType.UInt64;
			SqlMapper.typeMap[typeof(float).TypeHandle] = DbType.Single;
			SqlMapper.typeMap[typeof(double).TypeHandle] = DbType.Double;
			SqlMapper.typeMap[typeof(decimal).TypeHandle] = DbType.Decimal;
			SqlMapper.typeMap[typeof(bool).TypeHandle] = DbType.Boolean;
			SqlMapper.typeMap[typeof(string).TypeHandle] = DbType.String;
			SqlMapper.typeMap[typeof(char).TypeHandle] = DbType.StringFixedLength;
			SqlMapper.typeMap[typeof(Guid).TypeHandle] = DbType.Guid;
			SqlMapper.typeMap[typeof(DateTime).TypeHandle] = DbType.DateTime;
			SqlMapper.typeMap[typeof(DateTimeOffset).TypeHandle] = DbType.DateTimeOffset;
			SqlMapper.typeMap[typeof(byte[]).TypeHandle] = DbType.Binary;
			SqlMapper.typeMap[typeof(byte?).TypeHandle] = DbType.Byte;
			SqlMapper.typeMap[typeof(sbyte?).TypeHandle] = DbType.SByte;
			SqlMapper.typeMap[typeof(short?).TypeHandle] = DbType.Int16;
			SqlMapper.typeMap[typeof(ushort?).TypeHandle] = DbType.UInt16;
			SqlMapper.typeMap[typeof(int?).TypeHandle] = DbType.Int32;
			SqlMapper.typeMap[typeof(uint?).TypeHandle] = DbType.UInt32;
			SqlMapper.typeMap[typeof(long?).TypeHandle] = DbType.Int64;
			SqlMapper.typeMap[typeof(ulong?).TypeHandle] = DbType.UInt64;
			SqlMapper.typeMap[typeof(float?).TypeHandle] = DbType.Single;
			SqlMapper.typeMap[typeof(double?).TypeHandle] = DbType.Double;
			SqlMapper.typeMap[typeof(decimal?).TypeHandle] = DbType.Decimal;
			SqlMapper.typeMap[typeof(bool?).TypeHandle] = DbType.Boolean;
			SqlMapper.typeMap[typeof(char?).TypeHandle] = DbType.StringFixedLength;
			SqlMapper.typeMap[typeof(Guid?).TypeHandle] = DbType.Guid;
			SqlMapper.typeMap[typeof(DateTime?).TypeHandle] = DbType.DateTime;
			SqlMapper.typeMap[typeof(DateTimeOffset?).TypeHandle] = DbType.DateTimeOffset;
		}

		private static DbType LookupDbType(Type type)
		{
			Type underlyingType = Nullable.GetUnderlyingType(type);
			if (underlyingType != null)
			{
				type = underlyingType;
			}
			if (type.IsEnum)
			{
				type = Enum.GetUnderlyingType(type);
			}
			DbType result;
			if (SqlMapper.typeMap.TryGetValue(type.TypeHandle, out result))
			{
				return result;
			}
			if (typeof(IEnumerable).IsAssignableFrom(type))
			{
				return DbType.Xml;
			}
			throw new NotSupportedException(string.Format("The type : {0} is not supported by dapper", type));
		}

		public static int Execute(this IDbConnection cnn, string sql, dynamic param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
		{
			IEnumerable enumerable = param as IEnumerable;
			SqlMapper.Identity identity;
			SqlMapper.CacheInfo cacheInfo;
			if (enumerable != null && !(enumerable is string))
			{
				Type[] interfaces = enumerable.GetType().GetInterfaces();
				Type typeFromHandle = typeof(IEnumerable<>);
				for (int i = 0; i < interfaces.Length; i++)
				{
					if (interfaces[i].IsGenericType && interfaces[i].GetGenericTypeDefinition() == typeFromHandle)
					{
						Type parametersType = interfaces[i].GetGenericArguments()[0];
						identity = new SqlMapper.Identity(sql, cnn, null, parametersType, null);
						cacheInfo = SqlMapper.GetCacheInfo(identity);
						using (IDbCommand dbCommand = SqlMapper.SetupCommand(cnn, transaction, sql, null, null, commandTimeout, commandType))
						{
							bool flag = true;
							string commandText = dbCommand.CommandText;
							Action<IDbCommand, object> paramReader = cacheInfo.ParamReader;
							int num = 0;
							foreach (object current in enumerable)
							{
								if (flag)
								{
									flag = false;
								}
								else
								{
									dbCommand.CommandText = commandText;
									dbCommand.Parameters.Clear();
								}
								paramReader(dbCommand, current);
								num += dbCommand.ExecuteNonQuery();
							}
							return num;
						}
					}
				}
			}

            

			identity = new SqlMapper.Identity(sql, cnn, null, (param == null) ? null : param.GetType(), null);
			cacheInfo = SqlMapper.GetCacheInfo(identity);
			return SqlMapper.ExecuteCommand(cnn, transaction, sql, cacheInfo.ParamReader, param, commandTimeout, commandType);
		}

        //[return: Dynamic(new bool[]
        //{
        //    false,
        //    true
        //})]
		public static IEnumerable<dynamic> Query(this IDbConnection cnn, string sql, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
		{
            return Query(cnn, sql, param, transaction, buffered, commandTimeout, commandType);
		}
        
        public static IEnumerable<T> Query<T>(this IDbConnection cnn, string sql, dynamic param = null
            , IDbTransaction transaction = null
            , bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
		{
            IEnumerable<T> enumerable = QueryInternal<T>(cnn, sql, param, transaction, commandTimeout, commandType);

			if (!buffered)
			{
				return enumerable;
			}
			return enumerable.ToList<T>();
		}
        

		public static SqlMapper.GridReader QueryMultiple(this IDbConnection cnn, string sql, dynamic param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
		{
			SqlMapper.Identity identity = new SqlMapper.Identity(sql, cnn, typeof(SqlMapper.GridReader), (param == null) ? null : param.GetType(), null);
			SqlMapper.CacheInfo cacheInfo = SqlMapper.GetCacheInfo(identity);
			IDbCommand dbCommand = null;
			IDataReader dataReader = null;
			SqlMapper.GridReader result;
			try
			{
				dbCommand = SqlMapper.SetupCommand(cnn, transaction, sql, cacheInfo.ParamReader, param, commandTimeout, commandType);
				dataReader = dbCommand.ExecuteReader();
				result = new SqlMapper.GridReader(dbCommand, dataReader, identity);
			}
			catch
			{
				if (dataReader != null)
				{
					dataReader.Dispose();
				}
				if (dbCommand != null)
				{
					dbCommand.Dispose();
				}
				throw;
			}
			return result;
		}

		private static IEnumerable<T> QueryInternal<T>(this IDbConnection cnn, string sql
            , object param, IDbTransaction transaction, int? commandTimeout
            , CommandType? commandType)
		{
			SqlMapper.Identity identity = new SqlMapper.Identity(sql, cnn, typeof(T), (param == null) ? null : param.GetType(), null);
			SqlMapper.CacheInfo cacheInfo = SqlMapper.GetCacheInfo(identity);
			bool flag = true;
			try
			{
				using (IDbCommand dbCommand = SqlMapper.SetupCommand(cnn, transaction, sql, cacheInfo.ParamReader, param, commandTimeout, commandType))
				{
					using (IDataReader dataReader = dbCommand.ExecuteReader())
					{
						if (cacheInfo.Deserializer == null)
						{
							cacheInfo.Deserializer = SqlMapper.GetDeserializer<T>(dataReader, 0, -1, false);
							SqlMapper.SetQueryCache(identity, cacheInfo);
						}
						Func<IDataReader, T> func = (Func<IDataReader, T>)cacheInfo.Deserializer;
						while (dataReader.Read())
						{
							flag = false;
							T t = func(dataReader);
							flag = true;
							yield return t;
						}
					}
				}
			}
			finally
			{
				if (!flag)
				{
					SqlMapper.PurgeQueryCache(identity);
				}
			}
			yield break;
		}
        
        /*
		public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
		{
			return cnn.MultiMap(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
		}
         * 
         **/ 
        /*
		public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
		{
			return cnn.MultiMap(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
		}
        */
        /*
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
        {
            return MultiMap<TFirst, TSecond, TThird, TFourth, TReturn>(cnn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        */
        /*
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
		{
            return MultiMap<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(cnn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
		}
        
        private static IEnumerable<TReturn> MultiMap<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection cnn, string sql, object map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
		{
            IEnumerable<TReturn> enumerable = cnn.MultiMapImpl<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(sql, map, param, transaction, splitOn, commandTimeout, commandType);
			if (!buffered)
			{
				return enumerable;
			}
			return enumerable.ToList<TReturn>();
		}
        */

        /// <summary>
        /// Maps a query to objects
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="cnn"></param>
        /// <param name="sql"></param>
        /// <param name="map"></param>
        /// <param name="param"></param>
        /// <param name="transaction"></param>
        /// <param name="buffered"></param>
        /// <param name="splitOn">The Field we should split and read the second object from (default: id)</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <returns></returns>
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(
#if CSHARP30  
            this IDbConnection cnn, string sql, Func<TFirst, TSecond, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType
#else
this IDbConnection cnn, string sql, Func<TFirst, TSecond, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null
#endif
)
        {
            return MultiMap<TFirst, TSecond, DontMap, DontMap, DontMap, TReturn>(cnn, sql, map, param as object, transaction, buffered, splitOn, commandTimeout, commandType);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(
#if CSHARP30
            this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType
#else
this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null
#endif
)
        {
            return MultiMap<TFirst, TSecond, TThird, DontMap, DontMap, TReturn>(cnn, sql, map, param as object, transaction, buffered, splitOn, commandTimeout, commandType);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(
#if CSHARP30
            this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType
#else
this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null
#endif
)
        {
            return MultiMap<TFirst, TSecond, TThird, TFourth, DontMap, TReturn>(cnn, sql, map, param as object, transaction, buffered, splitOn, commandTimeout, commandType);
        }
#if !CSHARP30
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
        {
            return MultiMap<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(cnn, sql, map, param as object, transaction, buffered, splitOn, commandTimeout, commandType);
        }
#endif
        
        static IEnumerable<TReturn> MultiMap<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(
            this IDbConnection cnn, string sql, object map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            var results = MultiMapImpl<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(cnn, sql, map, param, transaction, splitOn, commandTimeout, commandType);
            return buffered ? results.ToList() : results;
        }


		private static IEnumerable<TReturn> MultiMapImpl<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection cnn, string sql, object map, object param, IDbTransaction transaction, string splitOn, int? commandTimeout, CommandType? commandType)
		{
			SqlMapper.Identity identity = new SqlMapper.Identity(sql, cnn, typeof(TFirst), (param == null) ? null : param.GetType(), new Type[]
			{
				typeof(TFirst),
				typeof(TSecond),
				typeof(TThird),
				typeof(TFourth),
				typeof(TFifth)
			});
			SqlMapper.CacheInfo cacheInfo = SqlMapper.GetCacheInfo(identity);
			using (IDbCommand dbCommand = SqlMapper.SetupCommand(cnn, transaction, sql, cacheInfo.ParamReader, param, commandTimeout, commandType))
			{
				using (IDataReader reader = dbCommand.ExecuteReader())
				{
					if (cacheInfo.Deserializer == null)
					{
						int current = 0;
						string[] splits = splitOn.Split(new char[]
						{
							','
						}).ToArray<string>();
						int splitIndex = 0;
						Func<int> func = delegate
						{
							string b = splits[splitIndex];
							if (splits.Length > splitIndex + 1)
							{
								splitIndex++;
							}
							int num6 = current + 1;
							while (num6 < reader.FieldCount && !(splitOn == "*") && !string.Equals(reader.GetName(num6), b, StringComparison.InvariantCultureIgnoreCase))
							{
								num6++;
							}
							current = num6;
							return num6;
						};
						List<object> list = new List<object>();
						int num = func();
						cacheInfo.Deserializer = SqlMapper.GetDeserializer<TFirst>(reader, 0, num, false);
						if (typeof(TSecond) != typeof(SqlMapper.DontMap))
						{
							int num2 = func();
							list.Add(SqlMapper.GetDeserializer<TSecond>(reader, num, num2 - num, true));
							num = num2;
						}
						if (typeof(TThird) != typeof(SqlMapper.DontMap))
						{
							int num3 = func();
							list.Add(SqlMapper.GetDeserializer<TThird>(reader, num, num3 - num, true));
							num = num3;
						}
						if (typeof(TFourth) != typeof(SqlMapper.DontMap))
						{
							int num4 = func();
							list.Add(SqlMapper.GetDeserializer<TFourth>(reader, num, num4 - num, true));
							num = num4;
						}
						if (typeof(TFifth) != typeof(SqlMapper.DontMap))
						{
							int num5 = func();
							list.Add(SqlMapper.GetDeserializer<TFifth>(reader, num, num5 - num, true));
						}
						cacheInfo.OtherDeserializers = list.ToArray();
						SqlMapper.SetQueryCache(identity, cacheInfo);
					}
					Func<IDataReader, TFirst> deserializer = (Func<IDataReader, TFirst>)cacheInfo.Deserializer;
					Func<IDataReader, TSecond> deserializer2 = (Func<IDataReader, TSecond>)cacheInfo.OtherDeserializers[0];
					Func<IDataReader, TReturn> func2 = null;
					if (cacheInfo.OtherDeserializers.Length == 1)
					{
						func2 = ((IDataReader r) => ((Func<TFirst, TSecond, TReturn>)map)(deserializer(r), deserializer2(r)));
					}
					if (cacheInfo.OtherDeserializers.Length > 1)
					{
						Func<IDataReader, TThird> deserializer3 = (Func<IDataReader, TThird>)cacheInfo.OtherDeserializers[1];
						if (cacheInfo.OtherDeserializers.Length == 2)
						{
							func2 = ((IDataReader r) => ((Func<TFirst, TSecond, TThird, TReturn>)map)(deserializer(r), deserializer2(r), deserializer3(r)));
						}
						if (cacheInfo.OtherDeserializers.Length > 2)
						{
							Func<IDataReader, TFourth> deserializer4 = (Func<IDataReader, TFourth>)cacheInfo.OtherDeserializers[2];
							if (cacheInfo.OtherDeserializers.Length == 3)
							{
								func2 = ((IDataReader r) => ((Func<TFirst, TSecond, TThird, TFourth, TReturn>)map)(deserializer(r), deserializer2(r), deserializer3(r), deserializer4(r)));
							}
							if (cacheInfo.OtherDeserializers.Length > 3)
							{
								Func<IDataReader, TFifth> deserializer5 = (Func<IDataReader, TFifth>)cacheInfo.OtherDeserializers[3];
								func2 = ((IDataReader r) => ((Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>)map)(deserializer(r), deserializer2(r), deserializer3(r), deserializer4(r), deserializer5(r)));
							}
						}
					}
					if (func2 != null)
					{
						bool flag = true;
						try
						{
							while (reader.Read())
							{
								flag = false;
								TReturn tReturn = func2(reader);
								flag = true;
								yield return tReturn;
							}
						}
						finally
						{
							if (!flag)
							{
								SqlMapper.PurgeQueryCache(identity);
							}
						}
					}
				}
			}
			yield break;
		}

		private static SqlMapper.CacheInfo GetCacheInfo(SqlMapper.Identity identity)
		{
			SqlMapper.CacheInfo cacheInfo;
			if (!SqlMapper.TryGetQueryCache(identity, out cacheInfo))
			{
				cacheInfo = new SqlMapper.CacheInfo();
				if (identity.parametersType != null)
				{
					if (typeof(SqlMapper.IDynamicParameters).IsAssignableFrom(identity.parametersType))
					{
						cacheInfo.ParamReader = delegate(IDbCommand cmd, object obj)
						{
							(obj as SqlMapper.IDynamicParameters).AddParameters(cmd);
						};
					}
					else
					{
						cacheInfo.ParamReader = SqlMapper.CreateParamInfoGenerator(identity.parametersType);
					}
				}
				SqlMapper.SetQueryCache(identity, cacheInfo);
			}
			return cacheInfo;
		}

		private static Func<IDataReader, T> GetDeserializer<T>(IDataReader reader, int startBound, int length, bool returnNullIfFirstMissing)
		{
			Type typeFromHandle = typeof(T);
			if (typeFromHandle == typeof(object) || typeFromHandle == typeof(SqlMapper.FastExpando))
			{
				return SqlMapper.GetDynamicDeserializer<T>(reader, startBound, length, returnNullIfFirstMissing);
			}
			if (typeFromHandle.IsClass && typeFromHandle != typeof(string) && typeFromHandle != typeof(byte[]))
			{
				return SqlMapper.GetClassDeserializer<T>(reader, startBound, length, returnNullIfFirstMissing);
			}
			return SqlMapper.GetStructDeserializer<T>(startBound);
		}

		private static Func<IDataReader, T> GetDynamicDeserializer<T>(IDataRecord reader, int startBound, int length, bool returnNullIfFirstMissing)
		{
			if (length == -1)
			{
				length = reader.FieldCount - startBound;
			}
			return delegate(IDataReader r)
			{
				IDictionary<string, object> dictionary = new Dictionary<string, object>(length);
				for (int i = startBound; i < startBound + length; i++)
				{
					object obj = r.GetValue(i);
					obj = ((obj == DBNull.Value) ? null : obj);
					dictionary[r.GetName(i)] = obj;
					if (returnNullIfFirstMissing && i == startBound && obj == null)
					{
						return default(T);
					}
				}
				return (T)((object)SqlMapper.FastExpando.Attach(dictionary));
			};
		}

		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never), Obsolete("This method is for internal usage only", true)]
		public static void PackListParameters(IDbCommand command, string namePrefix, object value)
		{
			IEnumerable enumerable = value as IEnumerable;
			int count = 0;
			if (enumerable != null)
			{
				bool flag = value is IEnumerable<string>;
				foreach (object current in enumerable)
				{
					count++;
					IDbDataParameter dbDataParameter = command.CreateParameter();
					dbDataParameter.ParameterName = namePrefix + count;
					dbDataParameter.Value = (current ?? DBNull.Value);
					if (flag)
					{
						dbDataParameter.Size = 4000;
						if (current != null && ((string)current).Length > 4000)
						{
							dbDataParameter.Size = -1;
						}
					}
					command.Parameters.Add(dbDataParameter);
				}
				if (count == 0)
				{
					command.CommandText = Regex.Replace(command.CommandText, "[?@:]" + Regex.Escape(namePrefix), "(SELECT NULL WHERE 1 = 0)");
					return;
				}
				command.CommandText = Regex.Replace(command.CommandText, "[?@:]" + Regex.Escape(namePrefix), delegate(Match match)
				{
					string value2 = match.Value;
					StringBuilder stringBuilder = new StringBuilder("(").Append(value2).Append(1);
					for (int i = 2; i <= count; i++)
					{
						stringBuilder.Append(',').Append(value2).Append(i);
					}
					return stringBuilder.Append(')').ToString();
				});
			}
		}

		private static Action<IDbCommand, object> CreateParamInfoGenerator(Type type)
		{
			DynamicMethod dynamicMethod = new DynamicMethod(string.Format("ParamInfo{0}", Guid.NewGuid()), null, new Type[]
			{
				typeof(IDbCommand),
				typeof(object)
			}, type, true);
			ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
			iLGenerator.DeclareLocal(type);
			bool flag = false;
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Unbox_Any, type);
			iLGenerator.Emit(OpCodes.Stloc_0);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.EmitCall(OpCodes.Callvirt, typeof(IDbCommand).GetProperty("Parameters").GetGetMethod(), null);
			foreach (PropertyInfo current in from p in type.GetProperties()
			orderby p.Name
			select p)
			{
				if (current.PropertyType == typeof(DbString))
				{
					iLGenerator.Emit(OpCodes.Ldloc_0);
					iLGenerator.Emit(OpCodes.Callvirt, current.GetGetMethod());
					iLGenerator.Emit(OpCodes.Ldarg_0);
					iLGenerator.Emit(OpCodes.Ldstr, current.Name);
					iLGenerator.EmitCall(OpCodes.Callvirt, typeof(DbString).GetMethod("AddParameter"), null);
				}
				else
				{
					DbType dbType = SqlMapper.LookupDbType(current.PropertyType);
					if (dbType == DbType.Xml)
					{
						iLGenerator.Emit(OpCodes.Ldarg_0);
						iLGenerator.Emit(OpCodes.Ldstr, current.Name);
						iLGenerator.Emit(OpCodes.Ldloc_0);
						iLGenerator.Emit(OpCodes.Callvirt, current.GetGetMethod());
						if (current.PropertyType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, current.PropertyType);
						}
						iLGenerator.EmitCall(OpCodes.Call, typeof(SqlMapper).GetMethod("PackListParameters"), null);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Dup);
						iLGenerator.Emit(OpCodes.Ldarg_0);
						iLGenerator.EmitCall(OpCodes.Callvirt, typeof(IDbCommand).GetMethod("CreateParameter"), null);
						iLGenerator.Emit(OpCodes.Dup);
						iLGenerator.Emit(OpCodes.Ldstr, current.Name);
						iLGenerator.EmitCall(OpCodes.Callvirt, typeof(IDataParameter).GetProperty("ParameterName").GetSetMethod(), null);
						iLGenerator.Emit(OpCodes.Dup);
						SqlMapper.EmitInt32(iLGenerator, (int)dbType);
						iLGenerator.EmitCall(OpCodes.Callvirt, typeof(IDataParameter).GetProperty("DbType").GetSetMethod(), null);
						iLGenerator.Emit(OpCodes.Dup);
						SqlMapper.EmitInt32(iLGenerator, 1);
						iLGenerator.EmitCall(OpCodes.Callvirt, typeof(IDataParameter).GetProperty("Direction").GetSetMethod(), null);
						iLGenerator.Emit(OpCodes.Dup);
						iLGenerator.Emit(OpCodes.Ldloc_0);
						iLGenerator.Emit(OpCodes.Callvirt, current.GetGetMethod());
						bool flag2 = true;
						if (current.PropertyType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, current.PropertyType);
							if (Nullable.GetUnderlyingType(current.PropertyType) == null)
							{
								flag2 = false;
							}
						}
						if (flag2)
						{
							if (dbType == DbType.String && !flag)
							{
								iLGenerator.DeclareLocal(typeof(int));
								flag = true;
							}
							iLGenerator.Emit(OpCodes.Dup);
							Label label = iLGenerator.DefineLabel();
							Label? label2 = (dbType == DbType.String) ? new Label?(iLGenerator.DefineLabel()) : null;
							iLGenerator.Emit(OpCodes.Brtrue_S, label);
							iLGenerator.Emit(OpCodes.Pop);
							iLGenerator.Emit(OpCodes.Ldsfld, typeof(DBNull).GetField("Value"));
							if (dbType == DbType.String)
							{
								SqlMapper.EmitInt32(iLGenerator, 0);
								iLGenerator.Emit(OpCodes.Stloc_1);
							}
							if (label2.HasValue)
							{
								iLGenerator.Emit(OpCodes.Br_S, label2.Value);
							}
							iLGenerator.MarkLabel(label);
							if (current.PropertyType == typeof(string))
							{
								iLGenerator.Emit(OpCodes.Dup);
								iLGenerator.EmitCall(OpCodes.Callvirt, typeof(string).GetProperty("Length").GetGetMethod(), null);
								SqlMapper.EmitInt32(iLGenerator, 4000);
								iLGenerator.Emit(OpCodes.Cgt);
								Label label3 = iLGenerator.DefineLabel();
								Label label4 = iLGenerator.DefineLabel();
								iLGenerator.Emit(OpCodes.Brtrue_S, label3);
								SqlMapper.EmitInt32(iLGenerator, 4000);
								iLGenerator.Emit(OpCodes.Br_S, label4);
								iLGenerator.MarkLabel(label3);
								SqlMapper.EmitInt32(iLGenerator, -1);
								iLGenerator.MarkLabel(label4);
								iLGenerator.Emit(OpCodes.Stloc_1);
							}
							if (label2.HasValue)
							{
								iLGenerator.MarkLabel(label2.Value);
							}
						}
						iLGenerator.EmitCall(OpCodes.Callvirt, typeof(IDataParameter).GetProperty("Value").GetSetMethod(), null);
						if (current.PropertyType == typeof(string))
						{
							Label label5 = iLGenerator.DefineLabel();
							iLGenerator.Emit(OpCodes.Ldloc_1);
							iLGenerator.Emit(OpCodes.Brfalse_S, label5);
							iLGenerator.Emit(OpCodes.Dup);
							iLGenerator.Emit(OpCodes.Ldloc_1);
							iLGenerator.EmitCall(OpCodes.Callvirt, typeof(IDbDataParameter).GetProperty("Size").GetSetMethod(), null);
							iLGenerator.MarkLabel(label5);
						}
						iLGenerator.EmitCall(OpCodes.Callvirt, typeof(IList).GetMethod("Add"), null);
						iLGenerator.Emit(OpCodes.Pop);
					}
				}
			}
			iLGenerator.Emit(OpCodes.Pop);
			iLGenerator.Emit(OpCodes.Ret);
			return (Action<IDbCommand, object>)dynamicMethod.CreateDelegate(typeof(Action<IDbCommand, object>));
		}

		private static IDbCommand SetupCommand(IDbConnection cnn, IDbTransaction transaction, string sql, Action<IDbCommand, object> paramReader, object obj, int? commandTimeout, CommandType? commandType)
		{
			IDbCommand dbCommand = cnn.CreateCommand();
			Action<IDbCommand, bool> bindByName = SqlMapper.GetBindByName(dbCommand.GetType());
			if (bindByName != null)
			{
				bindByName(dbCommand, true);
			}
			dbCommand.Transaction = transaction;
			dbCommand.CommandText = sql;
			if (commandTimeout.HasValue)
			{
				dbCommand.CommandTimeout = commandTimeout.Value;
			}
			if (commandType.HasValue)
			{
				dbCommand.CommandType = commandType.Value;
			}
			if (paramReader != null)
			{
				paramReader(dbCommand, obj);
			}
			return dbCommand;
		}

		private static int ExecuteCommand(IDbConnection cnn, IDbTransaction tranaction, string sql, Action<IDbCommand, object> paramReader, object obj, int? commandTimeout, CommandType? commandType)
		{
			int result;
			using (IDbCommand dbCommand = SqlMapper.SetupCommand(cnn, tranaction, sql, paramReader, obj, commandTimeout, commandType))
			{
				result = dbCommand.ExecuteNonQuery();
			}
			return result;
		}

		private static Func<IDataReader, T> GetStructDeserializer<T>(int index)
		{
			return delegate(IDataReader r)
			{
				object obj = r.GetValue(index);
				if (obj == DBNull.Value)
				{
					obj = null;
				}
				return (T)((object)obj);
			};
		}

		public static Func<IDataReader, T> GetClassDeserializer<T>(IDataReader reader, int startBound = 0, int length = -1, bool returnNullIfFirstMissing = false)
		{
			DynamicMethod dynamicMethod = new DynamicMethod(string.Format("Deserialize{0}", Guid.NewGuid()), typeof(T), new Type[]
			{
				typeof(IDataReader)
			}, true);
			ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
			iLGenerator.DeclareLocal(typeof(int));
			iLGenerator.DeclareLocal(typeof(T));
			bool flag = false;
			iLGenerator.Emit(OpCodes.Ldc_I4_0);
			iLGenerator.Emit(OpCodes.Stloc_0);
			var properties = (from p in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Name = p.Name,
				Setter = ((p.DeclaringType == typeof(T)) ? p.GetSetMethod(true) : p.DeclaringType.GetProperty(p.Name).GetSetMethod(true)),
				Type = p.PropertyType
			} into info
			where info.Setter != null
			select info).ToList();
			FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (length == -1)
			{
				length = reader.FieldCount - startBound;
			}
			List<string> list = new List<string>();
			for (int i = startBound; i < startBound + length; i++)
			{
				list.Add(reader.GetName(i));
			}
			var list2 = (from n in list
			let prop = properties.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.InvariantCulture)) ?? properties.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.InvariantCultureIgnoreCase))
			let field = (prop != null) ? null : (fields.FirstOrDefault((FieldInfo p) => string.Equals(p.Name, n, StringComparison.InvariantCulture)) ?? fields.FirstOrDefault((FieldInfo p) => string.Equals(p.Name, n, StringComparison.InvariantCultureIgnoreCase)))
			select new
			{
				Name = n,
				Property = prop,
				Field = field
			}).ToList();
			int num = startBound;
			iLGenerator.BeginExceptionBlock();
			iLGenerator.Emit(OpCodes.Newobj, typeof(T).GetConstructor(Type.EmptyTypes));
			bool flag2 = true;
			Label label = iLGenerator.DefineLabel();
			foreach (var current in list2)
			{
				if (current.Property != null || current.Field != null)
				{
					iLGenerator.Emit(OpCodes.Dup);
					Label label2 = iLGenerator.DefineLabel();
					Label label3 = iLGenerator.DefineLabel();
					iLGenerator.Emit(OpCodes.Ldarg_0);
					SqlMapper.EmitInt32(iLGenerator, num);
					iLGenerator.Emit(OpCodes.Dup);
					iLGenerator.Emit(OpCodes.Stloc_0);
					iLGenerator.Emit(OpCodes.Callvirt, SqlMapper.getItem);
					iLGenerator.Emit(OpCodes.Dup);
					iLGenerator.Emit(OpCodes.Isinst, typeof(DBNull));
					iLGenerator.Emit(OpCodes.Brtrue_S, label2);
					Type type = (current.Property != null) ? current.Property.Type : current.Field.FieldType;
					Type underlyingType = Nullable.GetUnderlyingType(type);
					Type type2 = (underlyingType != null && underlyingType.IsEnum) ? underlyingType : type;
					if (type2.IsEnum)
					{
						if (!flag)
						{
							iLGenerator.DeclareLocal(typeof(string));
							flag = true;
						}
						Label label4 = iLGenerator.DefineLabel();
						iLGenerator.Emit(OpCodes.Dup);
						iLGenerator.Emit(OpCodes.Isinst, typeof(string));
						iLGenerator.Emit(OpCodes.Dup);
						iLGenerator.Emit(OpCodes.Stloc_2);
						iLGenerator.Emit(OpCodes.Brfalse_S, label4);
						iLGenerator.Emit(OpCodes.Pop);
						iLGenerator.Emit(OpCodes.Ldtoken, type2);
						iLGenerator.EmitCall(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"), null);
						iLGenerator.Emit(OpCodes.Ldloc_2);
						iLGenerator.Emit(OpCodes.Ldc_I4_1);
						iLGenerator.EmitCall(OpCodes.Call, SqlMapper.enumParse, null);
						iLGenerator.Emit(OpCodes.Unbox_Any, type2);
						if (underlyingType != null)
						{
							iLGenerator.Emit(OpCodes.Newobj, type.GetConstructor(new Type[]
							{
								underlyingType
							}));
						}
						if (current.Property != null)
						{
							iLGenerator.Emit(OpCodes.Callvirt, current.Property.Setter);
						}
						else
						{
							iLGenerator.Emit(OpCodes.Stfld, current.Field);
						}
						iLGenerator.Emit(OpCodes.Br_S, label3);
						iLGenerator.MarkLabel(label4);
					}
					iLGenerator.Emit(OpCodes.Unbox_Any, type2);
					if (underlyingType != null && underlyingType.IsEnum)
					{
						iLGenerator.Emit(OpCodes.Newobj, type.GetConstructor(new Type[]
						{
							underlyingType
						}));
					}
					if (current.Property != null)
					{
						iLGenerator.Emit(OpCodes.Callvirt, current.Property.Setter);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Stfld, current.Field);
					}
					iLGenerator.Emit(OpCodes.Br_S, label3);
					iLGenerator.MarkLabel(label2);
					iLGenerator.Emit(OpCodes.Pop);
					iLGenerator.Emit(OpCodes.Pop);
					if (flag2 && returnNullIfFirstMissing)
					{
						iLGenerator.Emit(OpCodes.Pop);
						iLGenerator.Emit(OpCodes.Ldnull);
						iLGenerator.Emit(OpCodes.Stloc_1);
						iLGenerator.Emit(OpCodes.Br, label);
					}
					iLGenerator.MarkLabel(label3);
				}
				flag2 = false;
				num++;
			}
			iLGenerator.Emit(OpCodes.Stloc_1);
			iLGenerator.MarkLabel(label);
			iLGenerator.BeginCatchBlock(typeof(Exception));
			iLGenerator.Emit(OpCodes.Ldloc_0);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.EmitCall(OpCodes.Call, typeof(SqlMapper).GetMethod("ThrowDataException"), null);
			iLGenerator.Emit(OpCodes.Ldnull);
			iLGenerator.Emit(OpCodes.Stloc_1);
			iLGenerator.EndExceptionBlock();
			iLGenerator.Emit(OpCodes.Ldloc_1);
			iLGenerator.Emit(OpCodes.Ret);
			return (Func<IDataReader, T>)dynamicMethod.CreateDelegate(typeof(Func<IDataReader, T>));
		}

		public static void ThrowDataException(Exception ex, int index, IDataReader reader)
		{
			string arg = "(n/a)";
			string arg2 = "(n/a)";
			if (reader != null && index >= 0 && index < reader.FieldCount)
			{
				arg = reader.GetName(index);
				object value = reader.GetValue(index);
				if (value == null || value is DBNull)
				{
					arg2 = "<null>";
				}
				else
				{
					arg2 = Convert.ToString(value) + " - " + Type.GetTypeCode(value.GetType());
				}
			}
			throw new DataException(string.Format("Error parsing column {0} ({1}={2})", index, arg, arg2), ex);
		}

		private static void EmitInt32(ILGenerator il, int value)
		{
			switch (value)
			{
			case -1:
				il.Emit(OpCodes.Ldc_I4_M1);
				return;
			case 0:
				il.Emit(OpCodes.Ldc_I4_0);
				return;
			case 1:
				il.Emit(OpCodes.Ldc_I4_1);
				return;
			case 2:
				il.Emit(OpCodes.Ldc_I4_2);
				return;
			case 3:
				il.Emit(OpCodes.Ldc_I4_3);
				return;
			case 4:
				il.Emit(OpCodes.Ldc_I4_4);
				return;
			case 5:
				il.Emit(OpCodes.Ldc_I4_5);
				return;
			case 6:
				il.Emit(OpCodes.Ldc_I4_6);
				return;
			case 7:
				il.Emit(OpCodes.Ldc_I4_7);
				return;
			case 8:
				il.Emit(OpCodes.Ldc_I4_8);
				return;
			default:
				if (value >= -128 && value <= 127)
				{
					il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
					return;
				}
				il.Emit(OpCodes.Ldc_I4, value);
				return;
			}
		}
	}
}
