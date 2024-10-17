namespace SwaggerTrans.jv;

public static class Sample
{
    public const string ParamIn =   """
                                    import java.lang.annotation.Retention;
                                    import java.lang.annotation.RetentionPolicy;
                                    
                                    @Retention(RetentionPolicy.RUNTIME)
                                    public @interface ParamIn {
                                        ParamLocate where() default ParamLocate.Query;
                                    }
                                    """;
    public const string ParamLocate =   """
                                        public enum ParamLocate {
                                            Header,
                                            Query,
                                            Path,
                                            Cookie,
                                        }
                                        """;
    
    public const string Param =     """
                                    import androidx.annotation.NonNull;
                                    import com.google.gson.Gson;
                                    import java.lang.reflect.Field;
                                    import java.util.HashMap;
                                    import java.util.Map;

                                    public class Parameter {


                                        public Map<String, String> getHeaders() {
                                            return getMap(ParamLocate.Header);
                                        }

                                        public Map<String, String> getQuery() {
                                            return getMap(ParamLocate.Query);
                                        }

                                        public Map<String, String> getPath() {
                                            return getMap(ParamLocate.Path);
                                        }

                                        public Map<String, String> getCookie() {
                                            return getMap(ParamLocate.Cookie);
                                        }

                                        private Map<String, String> getMap(ParamLocate where) {
                                            HashMap<String, String> map = new HashMap<>();

                                            for (Field f: this.getClass().getFields()) {
                                                ParamIn ann = f.getAnnotation(ParamIn.class);
                                                if(ann == null) continue;
                                                if(ann.where() == where) {

                                                    try {
                                                        Object value = f.get(this);
                                                        if(value == null) continue;
                                                        if(value instanceof Iterable) {
                                                            for (Object obj : (Iterable<?>) value) {
                                                                map.put(f.getName()+"[]", obj.toString());
                                                            }
                                                        }else {
                                                            map.put(f.getName(), value.toString());
                                                        }
                                                    } catch (IllegalAccessException e) {
                                                        e.printStackTrace();
                                                        return new HashMap<>();
                                                    }
                                                }
                                            }

                                            return map;
                                        }


                                        @NonNull
                                        @Override
                                        public String toString() {
                                            return new Gson().toJson(this);
                                        }
                                    }

                                    """;

    public const string Client =    """
                                    import androidx.annotation.Nullable;
                                    import java.util.concurrent.CompletableFuture;
                                    import java.lang.reflect.Type;

                                    public interface Client {
                                        <T> CompletableFuture<T> sendGet(String url, @Nullable Parameter parameter, Type responseType);

                                        <T> CompletableFuture<T> sendPost(String url, @Nullable Parameter parameter, @Nullable String body, Type responseType);

                                        <T> CompletableFuture<T> sendPut(String url, @Nullable Parameter parameter, @Nullable String body, Type responseType);

                                        <T> CompletableFuture<T> sendPatch(String url, @Nullable Parameter parameter, @Nullable String body, Type responseType);

                                        <T> CompletableFuture<T> sendDelete(String url, @Nullable Parameter parameter, Type responseType);
                                    }
                                    """;
    public const string SampleClient =  """
                                        
                                        import android.content.Context;  
                                        import androidx.annotation.Nullable; 
                                        import com.android.volley.AuthFailureError;
                                        import com.android.volley.NetworkResponse;
                                        import com.android.volley.Request;
                                        import com.android.volley.RequestQueue;
                                        import com.android.volley.Response;
                                        import com.android.volley.toolbox.JsonRequest;
                                        import com.android.volley.toolbox.Volley;
                                        import com.google.gson.Gson;
                                        import java.util.HashMap;
                                        import java.util.Map;
                                        import java.util.concurrent.CompletableFuture;
                                        import java.lang.reflect.Type;


                                        public class RequestClient implements Client {

                                            private final String baseUrl = "";
                                            private final RequestQueue queue;

                                            public RequestClient(Context context) {
                                                this.queue = Volley.newRequestQueue(context);
                                            }

                                            @Override
                                            public <T> CompletableFuture<T> sendGet(String url, @Nullable Parameter parameter, Type responseType) {
                                                return send(Request.Method.GET, url, parameter, null, responseType);
                                            }

                                            @Override
                                            public <T> CompletableFuture<T> sendPost(String url, @Nullable Parameter parameter, @Nullable String body, Type responseType) {
                                                return send(Request.Method.POST, url, parameter, body, responseType);
                                            }

                                            @Override
                                            public <T> CompletableFuture<T> sendPut(String url, @Nullable Parameter parameter, @Nullable String body, Type responseType) {
                                                return send(Request.Method.PUT, url, parameter, body, responseType);
                                            }

                                            @Override
                                            public <T> CompletableFuture<T> sendPatch(String url, @Nullable Parameter parameter, @Nullable String body, Type responseType) {
                                                return send(Request.Method.PATCH, url, parameter, body, responseType);
                                            }

                                            @Override
                                            public <T> CompletableFuture<T> sendDelete(String url, @Nullable Parameter parameter, Type responseType) {
                                                return send(Request.Method.DELETE, url, parameter, null, responseType);
                                            }

                                            private <T> CompletableFuture<T> send(int method, String url,@Nullable Parameter parameter, @Nullable String body, Type responseType) {
                                                if(parameter != null)
                                                    for (Map.Entry<String, String> entry : parameter.getPath().entrySet()) {
                                                        url = url.replace("{" + entry.getKey() + "}", entry.getValue());
                                                    }
                                                CompletableFuture<T> future = new CompletableFuture<>();
                                                Request<String> request = new JsonRequest<String>(
                                                        method,
                                                        baseUrl + url,
                                                        body,
                                                        response -> {
                                                            try {
                                                                future.complete(new Gson().fromJson(response, responseType));
                                                            }catch (Exception e) {
                                                                future.completeExceptionally(e);
                                                            }
                                                        },
                                                        error -> {
                                                            future.completeExceptionally(error);
                                                        }
                                                ) {
                                                    @Override
                                                    protected Response<String> parseNetworkResponse(NetworkResponse response) {
                                                        String str = new String(response.data);
                                                        return Response.success(str, getCacheEntry());
                                                    }

                                                    @Nullable
                                                    @Override
                                                    protected Map<String, String> getParams() throws AuthFailureError {
                                                        if (parameter == null) return super.getParams();
                                                        return parameter.getQuery();
                                                    }

                                                    @Override
                                                    public Map<String, String> getHeaders() throws AuthFailureError {
                                                        if (parameter == null) return super.getHeaders();
                                                        Map<String, String> headers = parameter.getHeaders();
                                                        if (headers == null) headers = new HashMap<>();
                                                        Map<String, String> cookies = parameter.getCookie();
                                                        if (cookies != null) {
                                                            StringBuilder cookieString = new StringBuilder();
                                                            for (Map.Entry<String, String> entry : cookies.entrySet()) {
                                                                if (cookieString.length() > 0) {
                                                                    cookieString.append("; ");
                                                                }
                                                                cookieString.append(entry.getKey()).append("=").append(entry.getValue());
                                                            }
                                                            headers.put("Cookie", cookieString.toString());
                                                        }
                                                        return headers;
                                                    }
                                                };

                                                queue.add(request);

                                                return future;
                                            }

                                        }

                                        """;

    public const string HttpTrust = """
                                    import android.annotation.SuppressLint;

                                    import java.security.KeyManagementException;
                                    import java.security.NoSuchAlgorithmException;
                                    import java.security.SecureRandom;
                                    import java.security.cert.X509Certificate;

                                    import javax.net.ssl.HttpsURLConnection;
                                    import javax.net.ssl.SSLContext;
                                    import javax.net.ssl.TrustManager;
                                    import javax.net.ssl.X509TrustManager;

                                    @SuppressLint("CustomX509TrustManager")
                                    public class HttpsTrustManager implements X509TrustManager {

                                        private static TrustManager[] trustManagers;
                                        private static final X509Certificate[] _AcceptedIssuers = new X509Certificate[]{};

                                        @SuppressLint("TrustAllX509TrustManager")
                                        @Override
                                        public void checkClientTrusted(
                                                X509Certificate[] x509Certificates, String s) {

                                        }

                                        @SuppressLint("TrustAllX509TrustManager")
                                        @Override
                                        public void checkServerTrusted(
                                                X509Certificate[] x509Certificates, String s) {

                                        }

                                        public boolean isClientTrusted(X509Certificate[] chain) {
                                            return true;
                                        }

                                        public boolean isServerTrusted(X509Certificate[] chain) {
                                            return true;
                                        }

                                        @Override
                                        public X509Certificate[] getAcceptedIssuers() {
                                            return _AcceptedIssuers;
                                        }

                                        public static void allowAllSSL() {
                                            HttpsURLConnection.setDefaultHostnameVerifier((arg0, arg1) -> true);

                                            SSLContext context = null;
                                            if (trustManagers == null) {
                                                trustManagers = new TrustManager[]{new HttpsTrustManager()};
                                            }

                                            try {
                                                context = SSLContext.getInstance("TLS");
                                                context.init(null, trustManagers, new SecureRandom());
                                            } catch (NoSuchAlgorithmException | KeyManagementException e) {
                                                e.printStackTrace();
                                            }

                                            assert context != null;
                                            HttpsURLConnection.setDefaultSSLSocketFactory(context
                                                    .getSocketFactory());
                                        }

                                    }

                                    """;
}