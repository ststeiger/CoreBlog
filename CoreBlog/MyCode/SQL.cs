using System;

namespace CoreBlog
{
    public class SQL
    {
        public SQL()
        {
        }


        public static System.Data.Common.DbConnection CreateConnection()
        {
            return new System.Data.SqlClient.SqlConnection(SQL.GetConnectionString());
        }


        public static string GetConnectionString()
        {

            System.Data.SqlClient.SqlConnectionStringBuilder csb = new System.Data.SqlClient.SqlConnectionStringBuilder();

            csb.DataSource = @"VMSTZHDB\HBD_DBH";
            csb.InitialCatalog = "HBD_CAFM3_Produktiv";
            csb.IntegratedSecurity = true;

            return csb.ConnectionString;
        }


        public static string GetAssemblyQualifiedNoVersionName(System.Type type)
        {
            if (type == null)
                return null;

            return GetAssemblyQualifiedNoVersionName(type.AssemblyQualifiedName);
        } // End Function GetAssemblyQualifiedNoVersionName


        public static string GetAssemblyQualifiedNoVersionName(string input)
        {
            int i = 0;
            bool isNotFirst = false;
            for (; i < input.Length; ++i)
            {
                if (input[i] == ',')
                {
                    if (isNotFirst)
                        break;

                    isNotFirst = true;
                }
            }

            return input.Substring(0, i);
        } // End Function GetAssemblyQualifiedNoVersionName


    }
}

