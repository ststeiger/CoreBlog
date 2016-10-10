using System;
using System.Collections.Generic;
using System.Web;

namespace LargeObjectSerializer.ajax
{


    /// <summary>
    /// Zusammenfassungsbeschreibung für dataReceiver
    /// </summary>
    public class dataReceiver : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/plain";
            DeserializeMultipleLargeDataSets(context);
        }


        public static void DeserializeMultipleLargeDataSets(HttpContext context)
        {
            bool debug = true;
            string input = null;
            DataSetSerialization thisDataSet = null;

            if (context.Request.InputStream != null)
            {
                using (System.IO.StreamReader sr = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    if (debug)
                        input = sr.ReadToEnd();


                    if (context.Request.InputStream.CanSeek)
                        context.Request.InputStream.Position = 0;

                    // WARNING: Positioning thisDataSet outside the using-clause would dispose of the inputstream on debug=true...
                    thisDataSet = Deserialize<DataSetSerialization>(sr);
                } // End Using sr 

            } // End if (context.Request.InputStream != null)
                

            System.Console.WriteLine(input);
            System.Console.WriteLine(thisDataSet);
            context.Response.Write("SUCCESS");

            
            if (!string.IsNullOrEmpty(context.Request.ContentType))
                context.Response.Write("\r\nClient data content type " + context.Request.ContentType);
        } // End Sub DeserializeMultipleLargeDataSets 


        // .NET 4.5
        // [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(System.IO.StreamReader sr)
        {
            T ret;
            Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
            ret = (T)serializer.Deserialize(sr, typeof(T));

            serializer = null;
            return ret;
        } // End Function Deserialize 


        public class ColumnInfo
        {
            public string ColumnName;
            public System.Type FieldType;
        } // End Class ColumnInfo


        public class Table
        {
            public string Name;
            public System.Collections.Generic.List<ColumnInfo> Columns;
            public System.Collections.Generic.List<object[]> Rows;


            public Table()
            {
                this.Columns = new System.Collections.Generic.List<ColumnInfo>();
                this.Rows = new System.Collections.Generic.List<object[]>();
            } // End Constructor 

        } // End Class Table


        public class DataSetSerialization
        {
            public System.Collections.Generic.List<Table> Tables;


            public DataSetSerialization()
            {
                this.Tables = new System.Collections.Generic.List<Table>();
            } // End Constructor 

        } // End Class DataSetSerialization


        public bool IsReusable
        {
            get
            {
                return false;
            }
        } // End Property IsReusable 


    } // End Class dataReceiver : IHttpHandler


} // End Namespace LargeObjectSerializer.ajax
