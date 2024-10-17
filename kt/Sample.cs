namespace SwaggerTrans.kt;

public static class Sample
{
    public const string ParamIn =  """
                                   annotation class ParamIn(val where: ParamLocate )

                                   enum class ParamLocate {
                                       Header,
                                       Query,
                                       Path,
                                       Cookie,
                                   }
                                   """;
    
    public const string Param =  """
                                 import com.google.gson.Gson
                                 import kotlin.reflect.KClass
                                 import kotlin.reflect.full.createType
                                 import kotlin.reflect.full.findAnnotation
                                 import kotlin.reflect.full.isSubtypeOf
                                 import kotlin.reflect.full.memberProperties
                                 import kotlin.reflect.full.superclasses

                                 open class Parameter {

                                    fun getHeaders() : Map<String, String> {
                                        return getMap(this::class, this, ParamLocate.Header)
                                    }
                                    
                                    fun getQuery() : Map<String, String> {
                                        return getMap(this::class, this, ParamLocate.Query)
                                    }

                                    fun getPath() : Map<String, String> {
                                        return getMap(this::class, this, ParamLocate.Path)
                                    }

                                    fun getCookie() : Map<String, String> {
                                        return getMap(this::class, this, ParamLocate.Cookie)
                                    }

                                    private fun getMap(kClass: KClass<*>, instance: Any, where: ParamLocate): Map<String, String>  {
                                        val map = HashMap<String, String>()

                                        val props = kClass.memberProperties

                                        for (prop in props) {
                                            val ann = prop.findAnnotation<ParamIn>()
                                            if (ann == null || ann.where != where) continue
                                            if (prop.returnType.isSubtypeOf(Iterable::class.createType())) {
                                                val value = prop.getter.call(this) as? Iterable<*>
                                                if (value != null) {
                                                    for(item in value) {
                                                        map["${prop.name}[]"] = item.toString()
                                                    }
                                                }
                                            }
                                            else {
                                                if (prop.getter.call(instance) == null)
                                                    map[prop.name] = prop.getter.call(instance).toString()
                                            }
                                        }

                                        this::class.superclasses.forEach { superClass ->
                                            map.putAll(getMap(superClass, instance, where))
                                        }

                                        return map
                                    }
                                    
                                    override fun toString() : String {
                                        val gson = Gson()
                                        return gson.toJson(this)
                                    }
                                    
                                 }
                                 """;

    public const string Client =  """
                                  import java.lang.reflect.Type
                                  
                                  interface Client { 
                                       suspend fun <T> sendGet(url: String, parameter: Parameter?, responseType: Type): T
                                  
                                       suspend fun <T> sendPost(url: String, parameter: Parameter?, dataBody: String?, responseType: Type): T
                                  
                                       suspend fun <T> sendPut(url: String, parameter: Parameter?, dataBody: String?, responseType: Type): T
                                  
                                       suspend fun <T> sendPatch(url: String, parameter: Parameter?, dataBody: String?, responseType: Type): T
                                  
                                       suspend fun <T> sendDelete(url: String, parameter: Parameter?, responseType: Type): T

                                  }
                                  """;
    public const string SampleClient =  """
                                        import android.content.Context
                                        import com.android.volley.NetworkResponse
                                        import com.android.volley.Request
                                        import com.android.volley.Response
                                        import com.android.volley.toolbox.JsonRequest
                                        import com.android.volley.toolbox.Volley
                                        import com.google.gson.Gson 
                                        import kotlinx.coroutines.suspendCancellableCoroutine
                                        import kotlin.coroutines.resume
                                        import kotlin.coroutines.resumeWithException
                                        import java.lang.reflect.Type
                                        
                                        open class RequestClient(context: Context): Client {
                                                
                                            private val baseURL = ""
                                            private val queue = Volley.newRequestQueue(context)

                                            override suspend fun <T> sendGet(url: String, parameter: Parameter?, responseType: Type): T {
                                                return send(Request.Method.GET, url, parameter, null, responseType)
                                            }

                                            override suspend fun <T> sendPost(url: String, parameter: Parameter?, dataBody: String?, responseType: Type): T {
                                                return send(Request.Method.POST, url, parameter, dataBody, responseType)
                                            }

                                            override suspend fun <T> sendPut(url: String, parameter: Parameter?, dataBody: String?, responseType: Type): T {
                                                return send(Request.Method.PUT, url, parameter, dataBody, responseType)
                                            }

                                            override suspend fun <T> sendPatch(url: String, parameter: Parameter?, dataBody: String?, responseType: Type): T {
                                                return send(Request.Method.PATCH, url, parameter, dataBody, responseType)
                                            }

                                            override suspend fun <T> sendDelete(url: String, parameter: Parameter?, responseType: Type): T {
                                                return send(Request.Method.DELETE, url, parameter, null, responseType)
                                            }

                                            private suspend fun<T> send(method: Int, url: String, parameter: Parameter?, dataBody: String?, responseType: Type): T {
                                                for((key, value) in parameter?.getPath() ?: HashMap()) {
                                                    url.replace("{$key}", value)
                                                }

                                                return suspendCancellableCoroutine { cont ->
                                                    val request = object : JsonRequest<String>(
                                                        method,
                                                        baseURL + url,
                                                        dataBody,
                                                        { response ->
                                                            try {
                                                                cont.resume(Gson().fromJson(response, responseType))
                                                            } catch (e: Exception) {
                                                                cont.resumeWithException(e)
                                                            }
                                                        },
                                                        { error ->
                                                            cont.resumeWithException(error)
                                                        }
                                                    ){
                                                        override fun getParams(): Map<String, String>? {
                                                            return parameter?.getQuery()
                                                        }

                                                        override fun parseNetworkResponse(response: NetworkResponse?): Response<String> {
                                                            val responseString = response?.data?.let { String(it) }
                                                            return Response.success(responseString, null)
                                                        }

                                                        override fun getHeaders(): Map<String, String> {
                                                            val map = (parameter?.getHeaders() ?: HashMap()).toMutableMap()
                                                            val cookie = parameter?.getCookie()
                                                            if (cookie != null) {
                                                                map["Cookie"] = cookie.map { "${it.key}=${it.value}" }.joinToString("; ")
                                                            }
                                                            return map
                                                        }
                                                    }

                                                    queue.add(request)
                                                    cont.invokeOnCancellation {
                                                        request.cancel()
                                                    }
                                                }
                                            }

                                        }
                                        """;

    public const string HttpTrust = """
                                    import android.annotation.SuppressLint
                                    import java.security.KeyManagementException
                                    import java.security.NoSuchAlgorithmException
                                    import java.security.SecureRandom
                                    import java.security.cert.X509Certificate
                                    import javax.net.ssl.HttpsURLConnection
                                    import javax.net.ssl.SSLContext
                                    import javax.net.ssl.SSLSession
                                    import javax.net.ssl.TrustManager
                                    import javax.net.ssl.X509TrustManager


                                    @SuppressLint("CustomX509TrustManager")
                                    class HttpsTrustManager : X509TrustManager {
                                        @SuppressLint("TrustAllX509TrustManager")
                                        override fun checkClientTrusted(
                                            x509Certificates: Array<X509Certificate>, s: String
                                        ) {
                                        }

                                        @SuppressLint("TrustAllX509TrustManager")
                                        override fun checkServerTrusted(
                                            x509Certificates: Array<X509Certificate>, s: String
                                        ) {
                                        }

                                        fun isClientTrusted(chain: Array<X509Certificate?>?): Boolean {
                                            return true
                                        }

                                        fun isServerTrusted(chain: Array<X509Certificate?>?): Boolean {
                                            return true
                                        }

                                        override fun getAcceptedIssuers(): Array<X509Certificate> {
                                            return _AcceptedIssuers
                                        }

                                        companion object {
                                            private var trustManagers: Array<TrustManager>? = null
                                            private val _AcceptedIssuers = arrayOf<X509Certificate>()

                                            fun allowAllSSL() {
                                                HttpsURLConnection.setDefaultHostnameVerifier { arg0: String?, arg1: SSLSession? -> true }

                                                var context: SSLContext? = null
                                                if (trustManagers == null) {
                                                    trustManagers = arrayOf(HttpsTrustManager())
                                                }

                                                try {
                                                    context = SSLContext.getInstance("TLS")
                                                    context.init(null, trustManagers, SecureRandom())
                                                } catch (e: NoSuchAlgorithmException) {
                                                    e.printStackTrace()
                                                } catch (e: KeyManagementException) {
                                                    e.printStackTrace()
                                                }

                                                checkNotNull(context)
                                                HttpsURLConnection.setDefaultSSLSocketFactory(
                                                    context
                                                        .socketFactory
                                                )
                                            }
                                        }
                                    }
                                    """;
}