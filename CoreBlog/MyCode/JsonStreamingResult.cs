
namespace CoreBlog.MyCode
{

    // http://stackoverflow.com/questions/26269438/streaming-large-list-of-data-as-json-format-using-json-net
    public class JsonStreamingResult : System.Web.Mvc.ActionResult
    {
        private System.Collections.IEnumerable itemsToSerialize;

        public JsonStreamingResult(System.Collections.IEnumerable itemsToSerialize)
        {
            this.itemsToSerialize = itemsToSerialize;
        }

        public override void ExecuteResult(System.Web.Mvc.ControllerContext context)
        {
            System.Web.HttpResponseBase response = context.HttpContext.Response;
            response.ContentType = "application/json";
            response.ContentEncoding = System.Text.Encoding.UTF8;

            Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();

            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(response.OutputStream))
            {
                using (Newtonsoft.Json.JsonTextWriter writer = new Newtonsoft.Json.JsonTextWriter(sw))
                {
                    writer.WriteStartArray();
                    foreach (object item in itemsToSerialize)
                    {
                        Newtonsoft.Json.Linq.JObject obj = 
                            Newtonsoft.Json.Linq.JObject.FromObject(item, serializer);
                        obj.WriteTo(writer);
                        writer.Flush();
                    } // Next item 
                    writer.WriteEndArray();

                } // End using writer 

            } // End Using sw
        }
    }
}