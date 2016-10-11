
namespace CoreBlog
{


    public class SqlJsonResult  : System.Web.Mvc.ActionResult
    {
        private System.Data.Common.DbCommand m_cmd;
        private string m_sql;

        public SqlJsonResult(System.Data.Common.DbCommand cmdSelect)
        {
            this.m_cmd = cmdSelect;
        }

        public SqlJsonResult(string sql)
        {
            this.m_sql = sql;
        }


        public System.Data.Common.DbCommand GetCommand(System.Data.Common.DbConnection con)
        {
            if(this.m_cmd != null)
            {
                this.m_cmd.Connection = con;
                return this.m_cmd;
            }

            System.Data.Common.DbCommand cmd = con.CreateCommand();
            cmd.CommandText = this.m_sql;

            return cmd;
        }


        public override void ExecuteResult(System.Web.Mvc.ControllerContext context)
        {
            context.HttpContext.Response.ContentType = "application/json";
            context.HttpContext.Response.ContentEncoding = System.Text.Encoding.UTF8;


            using (Newtonsoft.Json.JsonTextWriter jsonWriter = new Newtonsoft.Json.JsonTextWriter(context.HttpContext.Response.Output))
            {
                jsonWriter.Formatting = Newtonsoft.Json.Formatting.Indented;


                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("Tables");
                jsonWriter.WriteStartArray();


                using (System.Data.Common.DbConnection con = SQL.CreateConnection())
                {
                    if (con.State != System.Data.ConnectionState.Open)
                        con.Open();

                    using (System.Data.Common.DbCommand cmd = this.GetCommand(con))
                    {
                        using (System.Data.Common.DbDataReader dr = cmd.ExecuteReader(System.Data.CommandBehavior.SequentialAccess
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
                                    jsonWriter.WriteValue(SQL.GetAssemblyQualifiedNoVersionName(dr.GetFieldType(i)));

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

            context.HttpContext.Response.Output.Flush();
            context.HttpContext.Response.OutputStream.Flush();
            context.HttpContext.Response.Flush();
        } // End Sub SerializeLargeDataset 


    }


} 
