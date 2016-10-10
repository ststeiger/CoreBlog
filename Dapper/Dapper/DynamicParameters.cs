using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace Dapper
{
	public class DynamicParameters : SqlMapper.IDynamicParameters
	{
		private class ParamInfo
		{
			public string Name
			{
				get;
				set;
			}

			public object Value
			{
				get;
				set;
			}

			public ParameterDirection ParameterDirection
			{
				get;
				set;
			}

			public DbType? DbType
			{
				get;
				set;
			}

			public int? Size
			{
				get;
				set;
			}

			public IDbDataParameter AttachedParam
			{
				get;
				set;
			}
		}

		private Dictionary<string, DynamicParameters.ParamInfo> parameters = new Dictionary<string, DynamicParameters.ParamInfo>();

		public DynamicParameters()
		{
		}

		public DynamicParameters(object template)
		{
			if (template != null)
			{
				PropertyInfo[] properties = template.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
				for (int i = 0; i < properties.Length; i++)
				{
					PropertyInfo propertyInfo = properties[i];
					if (propertyInfo.CanRead)
					{
						ParameterInfo[] indexParameters = propertyInfo.GetIndexParameters();
						if (indexParameters == null || indexParameters.Length == 0)
						{
							this.Add(propertyInfo.Name, propertyInfo.GetValue(template, null), null, new ParameterDirection?(ParameterDirection.Input), null);
						}
					}
				}
				FieldInfo[] fields = template.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
				for (int j = 0; j < fields.Length; j++)
				{
					FieldInfo fieldInfo = fields[j];
					this.Add(fieldInfo.Name, fieldInfo.GetValue(template), null, new ParameterDirection?(ParameterDirection.Input), null);
				}
			}
		}

		public void Add(string name, object value = null, DbType? dbType = null, ParameterDirection? direction = null, int? size = null)
		{
			this.parameters[DynamicParameters.Clean(name)] = new DynamicParameters.ParamInfo
			{
				Name = name,
				Value = value,
				ParameterDirection = (direction ?? ParameterDirection.Input),
				DbType = dbType,
				Size = size
			};
		}

		private static string Clean(string name)
		{
			if (!string.IsNullOrEmpty(name))
			{
				char c = name[0];
				if (c != ':')
				{
					switch (c)
					{
					case '?':
					case '@':
						break;
					default:
						return name;
					}
				}
				return name.Substring(1);
			}
			return name;
		}

		void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command)
		{
			foreach (DynamicParameters.ParamInfo current in this.parameters.Values)
			{
				IDbDataParameter dbDataParameter = command.CreateParameter();
				object value = current.Value;
				dbDataParameter.ParameterName = current.Name;
				dbDataParameter.Value = (value ?? DBNull.Value);
				dbDataParameter.Direction = current.ParameterDirection;
				string text = value as string;
				if (text != null && text.Length <= 4000)
				{
					dbDataParameter.Size = 4000;
				}
				if (current.Size.HasValue)
				{
					dbDataParameter.Size = current.Size.Value;
				}
				if (current.DbType.HasValue)
				{
					dbDataParameter.DbType = current.DbType.Value;
				}
				command.Parameters.Add(dbDataParameter);
				current.AttachedParam = dbDataParameter;
			}
		}

		public T Get<T>(string name)
		{
			return (T)((object)this.parameters[DynamicParameters.Clean(name)].AttachedParam.Value);
		}
	}
}
