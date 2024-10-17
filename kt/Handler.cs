using System.Text;
using Newtonsoft.Json.Linq;

namespace SwaggerTrans.kt;

public class Handler(string jsonStr) : BaseHandler(jsonStr)
{
    protected override void ApiHandler(string className, string rouVar, string path, JObject value)
    {
        RouteBuilder.AppendLine($"        const val {rouVar} = \"{path}\"");    
        foreach (var method in value)
        { 
           
            var finalClassName = className + method.Key.ToUpper();
            var builder = new StringBuilder();
            builder.AppendLine($"class {finalClassName} {{\n");
            builder.AppendLine();
            var contain = 0;
            if (method.Value is JObject content)
            { 
                RepoBuilder.Append($"    suspend fun {finalClassName}(");
                if (content["parameters"] is JArray parameters)
                {
                    contain += 1;
                    builder.AppendLine("    class Param(");
                    builder.AppendLine(ParametersHandle(parameters));
                    builder.AppendLine("    ): Parameter()\n"); 
                    RepoBuilder.Append($"parameter: {finalClassName}.Param,");
                }

                if (content["requestBody"] is JObject requestBody)
                {
                    contain += 2;
                    builder.AppendLine(RequestBodyHandle(finalClassName, requestBody));
                    switch (BodyType[finalClassName])
                    {
                        case "object":
                            RepoBuilder.Append($" body: {finalClassName}.Body");
                            break;
                        // case "array":
                        //     RepoBuilder.Append($" body: List<{finalClassName}.BodyData>");
                        //     break;
                        case "string": 
                            RepoBuilder.Append($" body: String");
                            break;
                        case "number":
                            RepoBuilder.Append($" body: Double");
                            break;
                        case "integer":
                            RepoBuilder.Append($" body: Int");
                            break;
                        case "boolean":
                            RepoBuilder.Append($" body: Boolean");
                            break;
                        default:
                            if (BodyType[finalClassName].Contains("array"))
                            {
                                var aType = BodyType[path].Split(";")[1];
                                if (aType.Contains("ref"))
                                {
                                    RepoBuilder.Append($" body: List<{finalClassName}.BodyData>");
                                }
                                else
                                {
                                    RepoBuilder.Append($" body: List<{aType}>");
                                } 
                            }
                            else
                            {
                                RepoBuilder.Append($" body: Any");
                            }
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
                    {// {finalClassName}.Data
                        "object" => $"{finalClassName}.Response", 
                        "string" => $"String",
                        "number" => $"Double",
                        "integer" => $"Int",
                        "boolean" => $"Boolean",
                        _ => $"Any",
                    };
                    if (ReturnType[finalClassName].Contains("array"))
                    {
                        var aType = ReturnType[finalClassName].Split(";")[1];
                        rType = aType.Contains("ref") ? $"Array<{finalClassName}.Data>" : $"Array<{aType}>";
                    }
                    RepoBuilder.Append($" ): {rType} {{ \n");
                    var bodyStr = "null";
                    var paramStr = "null"; 
                    switch (contain)
                    {
                        case 1:
                            paramStr = "parameter";
                            break;
                        case 2:
                            bodyStr = "Gson().toJson(body)";
                            break;
                        case 3:
                            bodyStr = "Gson().toJson(body)";
                            paramStr = "parameter";
                            break;
                    }
                    
                    RepoBuilder.Append($"       return client.send{ (char.ToUpper(method.Key[0]) + method.Key.ToLower().Substring(1))}(Routes.{rouVar}, {paramStr}");
                    if(method.Key.ToLower() == "post" || method.Key.ToLower() == "put" || method.Key.ToLower() == "patch")
                    {
                        RepoBuilder.Append($", {bodyStr}");
                    }
                    RepoBuilder.Append($", {rType}::class.java)\n");
                    RepoBuilder.AppendLine("    }\n");
                } 
            }
            
            builder.AppendLine($"}}");
            ModelTemp.Add(new Tuple<string, string>($"{PackageName}.model.{finalClassName}", builder.ToString()));
            
        }
    }

    protected override void GenFile()
    {
        RepoBuilder.Insert(0, "\nclass Repo(private val client: Client) {\n");
        RepoBuilder.Insert(0, $"import {PackageName}.model.*\n");
        RepoBuilder.Insert(0, "import com.google.gson.Gson\n");
        
        RouteBuilder.Insert(0,"    companion object {\n");
        RouteBuilder.Insert(0, "class Routes {\n");
        RouteBuilder.Insert(0, "\n");
        
        RouteBuilder.AppendLine("    }");
        RouteBuilder.AppendLine("}");
        RepoBuilder.AppendLine("}");
        
        files.Add("Client.kt", $"package {PackageName}\n\nimport {PackageName}.model.Parameter\n\n{Sample.Client}");
        files.Add("model/Parameter.kt", $"package {PackageName}.model\n\n{Sample.Param}");
        files.Add("model/ParamIn.kt", $"package {PackageName}.model\n\n{Sample.ParamIn}");
        files.Add("RequestClient.kt", $"package {PackageName}\n\nimport {PackageName}.model.Parameter\n{Sample.SampleClient}");
        files.Add("Repo.kt", $"package {PackageName}\n\n{RepoBuilder}");
        files.Add("Routes.kt", $"package {PackageName}\n\n{RouteBuilder}");
        files.Add("HttpsTrustManager.kt", $"package {PackageName}\n\n{Sample.HttpTrust}");
        
        foreach (var it in ModelTemp)
        {
            var split = it.Item1.Replace($"{PackageName}.", "").Split(".");
            if (split.Length == 1)
            {
                files.Add(split[0] + ".kt", $"package {PackageName}\n\n{it.Item2}"); 
            }
            else
            {
                files.Add($"model/{split[1]}" + ".kt", $"package {PackageName}.{split[0]}\n\n{it.Item2}"); 
            }
        }
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
            builder.AppendLine($"        val {value["name"]!.ToString().Replace("-", "_").Replace(".", "_").Replace("[", "").Replace("]", "")}: {GetType((value["schema"]! as JObject)!)},");
        }

        return builder.ToString();
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

        if (type != "object"){
            if (obj.ContainsKey("enum"))
            {
                builder.AppendLine($"   enum class {className} {{\n");
                foreach (var item in obj["enum"])
                {
                    builder.AppendLine($"        enum{item.ToString()},");
                } 
                builder.AppendLine("   }\n");
            }

            if (type == "array")
            {
                var items = obj["items"] as JObject;
            }
            return builder.ToString();
        }
        if (obj["properties"] is not JObject properties) return builder.ToString();
        builder.AppendLine($"   class {className}(");
        foreach (var property in properties)
        {
            if (property.Value is not JObject value) continue;
            builder.AppendLine($"        val {property.Key}: {GetType(value)},");
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
        builder.AppendLine("   )\n");
        foreach (var cls in moreClass.Where(cls => cls.Item2.Length > 0))
        {
            builder.AppendLine(cls.Item2);
        }

        return builder.ToString();

    }
    protected override string GetType(JObject? propertyDetails, bool isList = false)
    {
        if (propertyDetails == null) return "String?";
        var type = propertyDetails["type"]?.ToString();
        var nullStr = propertyDetails["nullable"]?.ToString() == "true" ? "?" : "";
        if (type == null)
        {
            var refs = propertyDetails["$ref"]?.ToString();
            if (refs == null) return "Any";
            return refs.Split("/").Last().Split('.').Last() + (isList ? ">" : "") + $"{nullStr},   // {refs}";
        }
        var jo = propertyDetails["items"] as JObject;
        return type switch
        {
            "string" => "String" + (isList ? ">" : "") + nullStr,
            "integer" => (propertyDetails["format"]?.ToString() == "int64" ? "Long" : "Int") + (isList ? ">" : "") + nullStr,
            "number" => "Double" + (isList ? ">" : "") + nullStr,
            "boolean" => "Boolean" + (isList ? ">" : "") + nullStr,
            "array" => jo == null ? "Any" : $"List<{GetType(jo, true)}",
            _ => "Any" // 默認處理
        };
        
        
    }
}