using System;
using System.Data;

namespace Dapper
{
	public sealed class DbString
	{
		public bool IsAnsi
		{
			get;
			set;
		}

		public bool IsFixedLength
		{
			get;
			set;
		}

		public int Length
		{
			get;
			set;
		}

		public string Value
		{
			get;
			set;
		}

		public DbString()
		{
			this.Length = -1;
		}

		public void AddParameter(IDbCommand command, string name)
		{
			if (this.IsFixedLength && this.Length == -1)
			{
				throw new InvalidOperationException("If specifying IsFixedLength,  a Length must also be specified");
			}
			IDbDataParameter dbDataParameter = command.CreateParameter();
			dbDataParameter.ParameterName = name;
            if (this.Value == null)
                dbDataParameter.Value = System.DBNull.Value;
            else
                dbDataParameter.Value = this.Value;
			
			if (this.Length == -1 && this.Value != null && this.Value.Length <= 4000)
			{
				dbDataParameter.Size = 4000;
			}
			else
			{
				dbDataParameter.Size = this.Length;
			}
			dbDataParameter.DbType = (this.IsAnsi ? (this.IsFixedLength ? DbType.AnsiStringFixedLength : DbType.AnsiString) : (this.IsFixedLength ? DbType.StringFixedLength : DbType.String));
			command.Parameters.Add(dbDataParameter);
		}
	}
}
