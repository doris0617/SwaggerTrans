using System.Text;
using Newtonsoft.Json.Linq;

namespace SwaggerTrans.jv;

public class Handler(string jsonStr) : BaseHandler(jsonStr)
{
    
    protected override void ApiHandler(string className, string rouVar, string path, JObject value)
    {
        RouteBuilder.AppendLine($"    public static String {rouVar} = \"{path}\";");    
        foreach (var method in value)
        { 
           
            var finalClassName = className + method.Key.ToUpper();
            var builder = new StringBuilder();
            builder.AppendLine($"public class {finalClassName} {{\n");
            builder.AppendLine();
            var contain = 0;
            if (method.Value is JObject content)
            { 
                RepoBuilder.Append($"    public CompletableFuture<$$res$$> {finalClassName}(");
                if (content["parameters"] is JArray parameters)
                {
                    contain += 1;
                    builder.AppendLine("    public static class Param extends Parameter {");
                    builder.AppendLine(ParametersHandle(parameters));
                    builder.AppendLine("    }\n"); 
                    RepoBuilder.Append($"{finalClassName}.Param parameter,");
                }

                if (content["requestBody"] is JObject requestBody)
                {
                    contain += 2;
                    builder.AppendLine(RequestBodyHandle(finalClassName, requestBody));
                    switch (BodyType[finalClassName])
                    {
                        case "object":
                            RepoBuilder.Append($" {finalClassName}.Body body");
                            break; 
                        case "string": 
                            RepoBuilder.Append($" String body");
                            break;
                        case "number":
                            RepoBuilder.Append($" double body");
                            break;
                        case "integer":
                            RepoBuilder.Append($" int body");
                            break;
                        case "boolean":
                            RepoBuilder.Append($" boolean body");
                            break;
                        default:
                            if (BodyType[finalClassName].Contains("array"))
                            {
                                var aType = BodyType[finalClassName].Split(";")[1];
                                if (aType.Contains("ref"))
                                {
                                    RepoBuilder.Append($" List<{finalClassName}.BodyData> body");
                                }
                                else
                                {
                                    RepoBuilder.Append($" List<{aType}> body");
                                } 
                            }
                            RepoBuilder.Append($" Object body");
                            break;
                        
                    } 
                }

                if (content["responses"] is JObject responses)
                {
                    builder.Append(ResponseHandle(finalClassName, responses));
                    
                    if (RepoBuilder[^1] == ',')
                    {
                        RepoBuilder.Remove(RepoBuilder.Length - 1, 1);
                    }
                    var rType = ReturnType[finalClassName] switch 
                    {
                        "object" => $"{finalClassName}.Response", 
                        "string" => $"String",
                        "number" => $"double",
                        "integer" => $"int",
                        "boolean" => $"boolean",
                        _ => $"Object",
                    }; 
                    if (ReturnType[finalClassName].Contains("array"))
                    {
                        var aType = ReturnType[finalClassName].Split(";")[1];
                        rType = aType.Contains("ref") ? $"{finalClassName}.Data[]" : $"{aType}[]";
                    }
                    RepoBuilder.Replace("$$res$$", rType);
                    RepoBuilder.Append($" ) {{ \n");
                    var bodyStr = "null";
                    var paramStr = "null"; 
                    switch (contain)
                    {
                        case 1:
                            paramStr = "parameter";
                            break;
                        case 2:
                            bodyStr = "new Gson().toJson(body)";
                            break;
                        case 3:
                            bodyStr = "new Gson().toJson(body)";
                            paramStr = "parameter";
                            break;
                    }
                    
                    RepoBuilder.Append($"        return client.send{ (char.ToUpper(method.Key[0]) + method.Key.ToLower().Substring(1))}(Routes.{rouVar}, {paramStr}");
                    if(method.Key.ToLower() == "post" || method.Key.ToLower() == "put" || method.Key.ToLower() == "patch")
                    {
                        RepoBuilder.Append($", {bodyStr}");
                    }
                    RepoBuilder.Append($", {rType}.class);\n");
                    RepoBuilder.AppendLine("    }\n");
                } 
            }
            
            builder.AppendLine($"}}");
            ModelTemp.Add(new Tuple<string, string>($"{PackageName}.model.{finalClassName}", builder.ToString()));
            
        }
    }

    protected override void GenFile()
    {
        RepoBuilder.Insert(0,   """
                                public class Repo {

                                    private final Client client;

                                    public Repo(Client client) {
                                        HttpsTrustManager.allowAllSSL();
                                        this.client = client;
                                    }

                                """);
        RepoBuilder.Insert(0, $"import {PackageName}.model.*;\n");
        RepoBuilder.Insert(0, "import com.google.gson.Gson;\n"); 
        RepoBuilder.Insert(0, "import java.lang.reflect.Type;\n");
        RepoBuilder.Insert(0, "import java.util.concurrent.CompletableFuture;\n");
         
        RouteBuilder.Insert(0, "public class Routes {\n");
        RouteBuilder.Insert(0, "\n");
         
        RouteBuilder.AppendLine("}");
        RepoBuilder.AppendLine("}");
        
        
        files.Add("Client.java", $"package {PackageName};\n\nimport {PackageName}.model.Parameter;\n\n{Sample.Client}");
        files.Add("model/Parameter.java", $"package {PackageName}.model;\n\n{Sample.Param}");
        files.Add("model/ParamLocate.java", $"package {PackageName}.model;\n\n{Sample.ParamLocate}");
        files.Add("model/ParamIn.java", $"package {PackageName}.model;\n\n{Sample.ParamIn}");
        files.Add("RequestClient.java", $"package {PackageName};\n\nimport {PackageName}.model.Parameter;\n{Sample.SampleClient}");
        files.Add("Repo.java", $"package {PackageName};\n\n{RepoBuilder}");
        files.Add("Routes.java", $"package {PackageName};\n\n{RouteBuilder}");
        files.Add("HttpsTrustManager.java", $"package {PackageName};\n\n{Sample.HttpTrust}");
        foreach (var it in ModelTemp)
        {
            var split = it.Item1.Replace($"{PackageName}.", "").Split(".");
            if (split.Length == 1)
            {
                files.Add(split[0] + ".java", $"package {PackageName};\n\nimport java.util.List;\n\n{it.Item2}");
            }
            else
            {
                files.Add($"model/{split[1]}" + ".java", $"package {PackageName}.{split[0]};\n\nimport java.util.List;\n\n{it.Item2}");
            }
        }
    } 

     
    protected override string SchemaHandle(string className, JObject obj)
    {
        var builder = new StringBuilder();
        var moreClass = new List<Tuple<string, string>>();
        if (obj.TryGetValue("$ref", out var refValue))
        {
            var refStr = refValue.ToString();
            return GetSchemaRefClass(className, refStr);
        } 
       
        var type = obj["type"]?.ToString();

        if (type != "object")
        {
            if (obj.ContainsKey("enum"))
            {
                builder.AppendLine($"   public enum {className} {{\n");
                foreach (var item in obj["enum"])
                {
                    builder.AppendLine($"        enum{item.ToString()},");
                } 
                builder.AppendLine("   }\n");
            }
            return builder.ToString();
        }
        if (obj["properties"] is not JObject properties) return builder.ToString();
        builder.AppendLine($"    public static class {className} {{\n");
        foreach (var property in properties)
        {
            if (property.Value is not JObject value) continue;
            builder.AppendLine($"        public {GetType(value)} {property.Key};");
            if (value.TryGetValue("$ref", out var value1))
            {
                var newRefStr = value1.ToString();
                var newClassName = newRefStr.Split("/").Last().Split('.').Last();
                if (className == newClassName) continue;
                moreClass.Add(new Tuple<string, string>(newClassName,
                    GetSchemaRefClass(newClassName, newRefStr)));
            }
            
            if (value["type"]?.ToString() != "array") continue;
            if (!value.TryGetValue("items", out var items)) continue;
            var itemsRef = items["$ref"]?.ToString();
            if (itemsRef == null) continue;
            var itemsClassName = itemsRef.Split("/").Last().Split('.').Last();
            if (className == itemsClassName) continue;
            moreClass.Add(new Tuple<string, string>(itemsClassName,
                GetSchemaRefClass(itemsClassName, itemsRef)));
        } 
        builder.AppendLine("   }\n");
        foreach (var cls in moreClass.Where(cls => cls.Item2.Length > 0))
        {
            builder.AppendLine(cls.Item2);
        }

        return builder.ToString();
    }
      
    private string ParametersHandle(JArray param)
    {
        var builder = new StringBuilder(); 
        foreach (var value in param.OfType<JObject>())
        { 
            if(!value.TryGetValue("in", out var value1)) continue;
            var inStr = value1.ToString().ToLower(); 
            inStr = char.ToUpper(inStr[0]) + inStr.Substring(1);
            builder.AppendLine($"        @ParamIn(where = ParamLocate.{inStr})");
            builder.AppendLine($"        public {GetType((value["schema"]! as JObject)!)} {value["name"]!.ToString().Replace("-", "_").Replace(".", "_").Replace("[", "").Replace("]", "")};");
        }

        return builder.ToString();
    }
    
    protected override string GetType(JObject? propertyDetails, bool isList = false)
    {
        if (propertyDetails == null) return "String";
        var type = propertyDetails["type"]?.ToString();
        if (type == null)
        {
            var refs = propertyDetails["$ref"]?.ToString();
            if (refs == null) return "Any";
            return refs.Split("/").Last().Split('.').Last() + (isList ? ">" : "") + $"   /* {refs} */";
        }
        var jo = propertyDetails["items"] as JObject;
        return type switch
        {
            "string" => "String" + (isList ? ">" : "") ,
            "integer" => (propertyDetails["format"]?.ToString() == "int64" ? "Long" : "Integer" ) + (isList ? ">" : ""),
            "number" => "double" + (isList ? ">" : "") ,
            "boolean" => "boolean" + (isList ? ">" : "") ,
            "array" => jo == null ? "Object" : $"List<{GetType(jo, true)}",
            _ => "Object" // 默認處理
        };
        
        
    }
}