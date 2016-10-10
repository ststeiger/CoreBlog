
using System.Web;


namespace LargeObjectSerializer.ajax
{


    /// <summary>
    /// Zusammenfassungsbeschreibung für serialize
    /// </summary>
    public class serialize : IHttpHandler
    {


        public static string GetConnectionString()
        {

            System.Data.SqlClient.SqlConnectionStringBuilder csb = new System.Data.SqlClient.SqlConnectionStringBuilder();

            csb.DataSource = @"VMSTZHDB\HBD_DBH";
            csb.InitialCatalog = "HBD_CAFM3_Produktiv";
            csb.IntegratedSecurity = true;

            return csb.ConnectionString;
        }


        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/plain";

#if (true)
                SerializeLargeDataset(context);
#else
                SerializeLargeTable(context);
#endif

        }


        public void SerializeLargeDataset(HttpContext context)
        {
            string strSQL = @"
SELECT TOP 10 * FROM T_Benutzer; 
SELECT TOP 10 * FROM T_Benutzergruppen; 

-- SELECT * FROM T_Benutzer LIMIT 10; 
-- SELECT * FROM T_Benutzergruppen LIMIT 10; 

-- SELECT * FROM T_Benutzer OFFSET 0 FETCH NEXT 10 ROWS ONLY;
-- SELECT * FROM T_Benutzergruppen OFFSET 0 FETCH NEXT 10 ROWS ONLY; 
";

            Newtonsoft.Json.JsonSerializer ser = new Newtonsoft.Json.JsonSerializer();

            using (Newtonsoft.Json.JsonTextWriter jsonWriter = new Newtonsoft.Json.JsonTextWriter(context.Response.Output))
            {
                jsonWriter.Formatting = Newtonsoft.Json.Formatting.Indented;


                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("Tables");
                jsonWriter.WriteStartArray();


                using (System.Data.SqlClient.SqlConnection con = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
                {
                    if (con.State != System.Data.ConnectionState.Open)
                        con.Open();

                    using (System.Data.SqlClient.SqlCommand cmd = con.CreateCommand())
                    {
                        cmd.CommandText = strSQL;

                        using (System.Data.SqlClient.SqlDataReader dr = cmd.ExecuteReader(System.Data.CommandBehavior.SequentialAccess
                             | System.Data.CommandBehavior.CloseConnection
                            ))
                        {

                            do
                            {
                                jsonWriter.WriteStartObject(); // tbl = new Table();

                                jsonWriter.WritePropertyName("Columns");
                                jsonWriter.WriteStartArray();


                                for (int i = 0; i < dr.FieldCount; ++i)
                                {
                                    jsonWriter.WriteStartObject();

                                    jsonWriter.WritePropertyName("ColumnName");
                                    jsonWriter.WriteValue(dr.GetName(i));

                                    jsonWriter.WritePropertyName("FieldType");
                                    jsonWriter.WriteValue(GetAssemblyQualifiedNoVersionName(dr.GetFieldType(i)));

                                    jsonWriter.WriteEndObject();
                                } // Next i 
                                jsonWriter.WriteEndArray();

                                jsonWriter.WritePropertyName("Rows");
                                jsonWriter.WriteStartArray();

                                if (dr.HasRows)
                                {

                                    while (dr.Read())
                                    {
                                        object[] thisRow = new object[dr.FieldCount];

                                        jsonWriter.WriteStartArray(); // object[] thisRow = new object[dr.FieldCount];
                                        for (int i = 0; i < dr.FieldCount; ++i)
                                        {
                                            jsonWriter.WriteValue(dr.GetValue(i));
                                        } // Next i
                                        jsonWriter.WriteEndArray(); // tbl.Rows.Add(thisRow);
                                    } // Whend 

                                } // End if (dr.HasRows) 

                                jsonWriter.WriteEndArray();

                                jsonWriter.WriteEndObject(); // ser.Tables.Add(tbl);
                            } while (dr.NextResult()); 
                           
                        } // End using dr 

                    } // End using cmd 

                    
                    if (con.State != System.Data.ConnectionState.Closed)
                        con.Close();
                } // End using con 

                jsonWriter.WriteEndArray();

                jsonWriter.WriteEndObject();
                jsonWriter.Flush();
            } // End Using jsonWriter 

            context.Response.Output.Flush();
            context.Response.OutputStream.Flush();
            context.Response.Flush();
        } // End Sub SerializeLargeDataset 


        public void SerializeLargeTable(HttpContext context)
        {
            Newtonsoft.Json.JsonSerializer ser = new Newtonsoft.Json.JsonSerializer();

            using (Newtonsoft.Json.JsonTextWriter jsonWriter = new Newtonsoft.Json.JsonTextWriter(context.Response.Output))
            {
                jsonWriter.Formatting = Newtonsoft.Json.Formatting.Indented;

                jsonWriter.WriteStartObject();


                using (System.Data.SqlClient.SqlConnection con = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
                {
                    if (con.State != System.Data.ConnectionState.Open)
                        con.Open();

                    using (System.Data.SqlClient.SqlCommand cmd = con.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 10000 * FROM T_LOG_SAP_Interface";

                        using (System.Data.SqlClient.SqlDataReader dr = cmd.ExecuteReader(System.Data.CommandBehavior.SequentialAccess
                             | System.Data.CommandBehavior.CloseConnection
                            ))
                        {


                            jsonWriter.WritePropertyName("Columns");
                            jsonWriter.WriteStartArray();



                            for (int i = 0; i < dr.FieldCount; i++)
                            {
                                string colName = dr.GetName(i);
                                System.Type t = dr.GetFieldType(i);

                                jsonWriter.WriteStartObject();

                                jsonWriter.WritePropertyName("ColumnName");
                                jsonWriter.WriteValue(colName);

                                jsonWriter.WritePropertyName("DataType");
                                jsonWriter.WriteValue(GetAssemblyQualifiedNoVersionName(t));

                                // jsonWriter.WritePropertyName("DateTimeMode");
                                // jsonWriter.WriteValue(column.DateTimeMode.ToString());

                                jsonWriter.WriteEndObject();
                            }


                            jsonWriter.WriteEndArray();


                            jsonWriter.WritePropertyName("Rows");
                            jsonWriter.WriteStartArray();


                            if (dr.HasRows)
                            {
                                int fieldCount = dr.FieldCount;

                                while (dr.Read())
                                {
                                    jsonWriter.WriteStartArray();

                                    for (int i = 0; i < fieldCount; ++i)
                                    {
                                        object obj = dr.GetValue(i);
                                        jsonWriter.WriteValue(obj);
                                    } // Next i

                                    jsonWriter.WriteEndArray();

                                    jsonWriter.Flush();
                                    context.Response.Output.Flush();
                                    context.Response.Flush();
                                } // Whend while (dr.Read())

                            } // End if (dr.HasRows)

                            dr.Close();
                            jsonWriter.WriteEndArray();
                        } // End using dr 
                         
                    } // End using cmd 

                    if (con.State != System.Data.ConnectionState.Closed)
                        con.Close();
                } // End using con 

                
                jsonWriter.WriteEndObject();

                jsonWriter.Flush();
                context.Response.Output.Flush();
                context.Response.OutputStream.Flush();
                context.Response.Flush();
            } // End Using jsonWriter 

        } // End Sub SerializeLargeTable 



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


        public bool IsReusable
        {
            get
            {
                return false;
            }
        } // End Property  IsReusable


    } // End Class serialize : IHttpHandler


} // End Namespace LargeObjectSerializer.ajax
