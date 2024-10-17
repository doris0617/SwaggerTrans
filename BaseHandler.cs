using System.Text;
using Newtonsoft.Json.Linq;

namespace SwaggerTrans
{
    public abstract class BaseHandler(string jsonStr)
    {
        private readonly JObject _data = JObject.Parse(jsonStr);
        internal string PackageName = "lib.utils";
        internal readonly Dictionary<string, string> ReturnType = new();
        internal readonly Dictionary<string, string> BodyType = new();
        public readonly List<Tuple<string, string>> ModelTemp = [];
        public readonly StringBuilder RepoBuilder = new();
        public readonly StringBuilder RouteBuilder = new();
        public readonly Dictionary<string, string> files = new();
        
        public void SetPackageName(string name)
        {
            PackageName = name;
        }
         
        public void GenDataModel()
        {
            // Reset Temp Data
            ReturnType.Clear();
            BodyType.Clear();
            ModelTemp.Clear();
            RepoBuilder.Clear();
            RouteBuilder.Clear();
            files.Clear();
 
            if (_data["paths"] is not JObject json) return;
            foreach (var (path, jToken) in json)
            {
                var rouVar = ConvertToConstValue(path);
                var className = ConvertToClassName(path);
                if (jToken is JObject value)
                {
                    ApiHandler(className, rouVar, path, value);
                }

            }   
            GenFile();
        }
        
        protected abstract void ApiHandler(string className, string rouVar, string path, JObject value);
        
        public void Save(string path)
        {
            var rootPath = Path.Combine(path, "swaggerTrans");
            if (!Directory.Exists(rootPath))
            {
                Directory.CreateDirectory(rootPath);
            }
            var modelPath = Path.Combine(rootPath, "model");
            if (!Directory.Exists(modelPath))
                Directory.CreateDirectory(modelPath);  
            if (!Directory.Exists(rootPath))
            {
                Directory.CreateDirectory(rootPath);
            }
            foreach (var it in files)
            {
                File.WriteAllText(Path.Combine(rootPath, it.Key), it.Value);
            }
        }

        protected abstract void GenFile();
         

        private string GetArrayType(Dictionary<string, string> dict, string path, string className, JObject items)
        {
             if (items["$ref"] is not null)
             {
                 dict.Add(path, "array;ref");
                 return GetSchemaRefClass(className, items["$ref"]!.ToString());
             }
            
             var type = items["type"]?.ToString();
             if (type is "array" or "object")
             {
                 dict.Add(path, "array;any");
                 if (type == "object")
                 {
                     return SchemaHandle(className, items);
                 }
             }else {
                 dict.Add(path, $"array;{GetType(items)}");
             }

             return "";
        }

        protected abstract string GetType(JObject? propertyDetails, bool isList = false);
        
        internal string RequestBodyHandle(string key, JObject requestBody)
        {  
            var content = requestBody["content"] as JObject;
            var schema = content?["application/json"]?["schema"] as JObject;
            if (schema == null) return "";
            if (schema.ContainsKey("$ref"))
            {
                BodyType.Add(key, "object");
                return SchemaHandle("Body", schema);
            }
            var type = schema["type"]?.ToString();
            if (type == "array")
            {
                // BodyType.Add(path, "array");
                if (schema["items"] is not JObject value)
                {
                    return "";
                }
                return GetArrayType( BodyType, key, "BodyData", value);
                // if (value["$ref"] is not null)
                // {
                //     return GetSchemaRefClass("BodyData", value["$ref"]!.ToString());
                // }
                // return "";
            }
            switch (type)
            {
                case null:
                    return "";
                case "object":
                    BodyType.Add(key, "object");
                    return SchemaHandle("Response", schema); 
                default:
                    BodyType.Add(key, type);
                    return "";
            }
        }

        internal string ResponseHandle(string key, JObject body)
        {
            if (!body.TryGetValue("200", out var value1))
            {
                ReturnType.Add(key, "string");
                return "";
            }
            var res200 = (value1 as JObject)!;
            Console.WriteLine(res200);
            if (!res200.TryGetValue("content", out var value2))
            {
                ReturnType.Add(key, "string");
                return "";
            }

            if (value2 is not JObject content)
            {
                ReturnType.Add(key, "string");
                return "";
            } 
            if(content["application/json"]?["schema"] is null)
            {
                ReturnType.Add(key, "string");
                return "";
            }
            if (content["application/json"]?["schema"] is not JObject schema)
            {
                ReturnType.Add(key, "string");
                return "";
            } 
            if (schema.ContainsKey("$ref"))
            { 
                ReturnType.Add(key, "object");
                return SchemaHandle("Response", schema);
            }
            var type = schema["type"]?.ToString();
            if (type == "array")
            {
                // ReturnType.Add(path, "array");
                if (schema["items"] is not JObject value)
                {
                    return "";
                }
                return GetArrayType(ReturnType, key, "Data", value);
                // if (value["$ref"] is not null)
                // {
                //     return GetSchemaRefClass("Data", value["$ref"]!.ToString());
                // }
                // return "";
            }
            ReturnType.Add(key, type ?? "");
            switch (type)
            {
                case null:
                    return "";
                case "object":
                    return SchemaHandle("Response", schema); 
                default: 
                    return "";
            }
        }

        protected abstract string SchemaHandle(string className, JObject obj);
        
        internal string GetSchemaRefClass(string className, string refStr)
        {  
            var split = refStr.Split("/"); 
            var obj = _data;
            for (var i = 1; i < split.Length; i++)
            {
                var key = split[i];
                if (obj[key] is JObject value)
                {
                    obj = value;
                }else {
                    throw new Exception("ref not found");
                }
            } 
            
            return SchemaHandle(className, obj);
        } 
        private static string ConvertToConstValue(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
 
            var segments = path.Replace("{", "").Replace("}", "").TrimStart('/').Split('/');
 
            var result = string.Join("_", segments.Select(segment => segment.ToUpper()));

            return result;
        }
        
        private static string ConvertToClassName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
 
            var segments = path.Replace("{", "").Replace("}", "").TrimStart('/').Split('/');
 
            var result = string.Join("", segments.Select(segment => char.ToUpper(segment[0]) + segment[1..]));

            return result;
        }
    }
}
