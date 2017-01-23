using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using ServiceStack.Host;
using ServiceStack.Host.Handlers;
using ServiceStack.Text;
using ServiceStack.Web;

namespace ServiceStack
{
    public class HttpHandlerFactory : IHttpHandlerFactory
    {
        static readonly HashSet<string> WebHostRootFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string WebHostPhysicalPath = null;
        private static string DefaultRootFileName = null;
        //internal static string ApplicationBaseUrl = null;
        private static IHttpHandler DefaultHttpHandler = null;
        private static RedirectHttpHandler NonRootModeDefaultHttpHandler = null;
        private static IHttpHandler ForbiddenHttpHandler = null;
        private static IHttpHandler NotFoundHttpHandler = null;
        private static readonly IHttpHandler StaticFilesHandler = new StaticFileHandler();
        private static bool IsIntegratedPipeline = false;
        private static bool HostAutoRedirectsDirs = false;

        [ThreadStatic]
        public static string DebugLastHandlerArgs;

        internal static void Init()
        {
            try
            {
#if !NETSTANDARD1_6
                //MONO doesn't implement this property
                var pi = typeof(HttpRuntime).GetProperty("UsingIntegratedPipeline");
                if (pi != null)
                {
                    IsIntegratedPipeline = (bool) pi.GetGetMethod().Invoke(null, TypeConstants.EmptyObjectArray);
                }
#endif
                var appHost = HostContext.AppHost;
                var config = appHost.Config;

                var isAspNetHost = HostContext.IsAspNetHost;
                WebHostPhysicalPath = appHost.VirtualFileSources.RootDirectory.RealPath;
                HostAutoRedirectsDirs = isAspNetHost && !Env.IsMono;

                //Apache+mod_mono treats path="servicestack*" as path="*" so takes over root path, so we need to serve matching resources
                var hostedAtRootPath = config.HandlerFactoryPath == null;

                //DefaultHttpHandler not supported in IntegratedPipeline mode
                if (!IsIntegratedPipeline && isAspNetHost && !hostedAtRootPath && !Env.IsMono)
                    DefaultHttpHandler = new DefaultHttpHandler();

                var rootFiles = appHost.VirtualFileSources.GetRootFiles().ToList();
                foreach (var file in rootFiles)
                {
                    var fileNameLower = file.Name.ToLowerInvariant();
                    if (DefaultRootFileName == null && config.DefaultDocuments.Contains(fileNameLower))
                    {
                        //Can't serve Default.aspx pages so ignore and allow for next default document
                        if (!fileNameLower.EndsWith(".aspx"))
                        {
                            DefaultRootFileName = fileNameLower;
                            StaticFileHandler.SetDefaultFile(file.VirtualPath, file.ReadAllBytes(), file.LastModified);

                            if (DefaultHttpHandler == null)
                                DefaultHttpHandler = new StaticFileHandler(file);
                        }
                    }
                    WebHostRootFileNames.Add(fileNameLower);
                }

                foreach (var dir in appHost.VirtualFileSources.GetRootDirectories())
                {
                    WebHostRootFileNames.Add(dir.Name.ToLowerInvariant());
                }

                if (!string.IsNullOrEmpty(config.DefaultRedirectPath))
                {
                    DefaultHttpHandler = new RedirectHttpHandler {RelativeUrl = config.DefaultRedirectPath};
                    NonRootModeDefaultHttpHandler = new RedirectHttpHandler {RelativeUrl = config.DefaultRedirectPath};
                }

                if (DefaultHttpHandler == null && !string.IsNullOrEmpty(config.MetadataRedirectPath))
                {
                    DefaultHttpHandler = new RedirectHttpHandler {RelativeUrl = config.MetadataRedirectPath};
                    NonRootModeDefaultHttpHandler = new RedirectHttpHandler {RelativeUrl = config.MetadataRedirectPath};
                }

                if (DefaultHttpHandler == null)
                    DefaultHttpHandler = NotFoundHttpHandler;

                var defaultRedirectHanlder = DefaultHttpHandler as RedirectHttpHandler;
                var debugDefaultHandler = defaultRedirectHanlder != null
                    ? defaultRedirectHanlder.RelativeUrl
                    : typeof(DefaultHttpHandler).GetOperationName();

                SetApplicationBaseUrl(config.WebHostUrl);

                ForbiddenHttpHandler = appHost.GetCustomErrorHttpHandler(HttpStatusCode.Forbidden);
                if (ForbiddenHttpHandler == null)
                {
                    ForbiddenHttpHandler = new ForbiddenHttpHandler
                    {
                        IsIntegratedPipeline = IsIntegratedPipeline,
                        WebHostPhysicalPath = WebHostPhysicalPath,
                        WebHostRootFileNames = WebHostRootFileNames.ToList(),
                        WebHostUrl = config.WebHostUrl,
                        DefaultRootFileName = DefaultRootFileName,
                        DefaultHandler = debugDefaultHandler,
                    };
                }

                NotFoundHttpHandler = appHost.GetCustomErrorHttpHandler(HttpStatusCode.NotFound);
                if (NotFoundHttpHandler == null)
                {
                    NotFoundHttpHandler = new NotFoundHttpHandler
                    {
                        IsIntegratedPipeline = IsIntegratedPipeline,
                        WebHostPhysicalPath = WebHostPhysicalPath,
                        WebHostRootFileNames = WebHostRootFileNames.ToList(),
                        WebHostUrl = config.WebHostUrl,
                        DefaultRootFileName = DefaultRootFileName,
                        DefaultHandler = debugDefaultHandler,
                    };
                }
            }
            catch (Exception ex)
            {
                HostContext.AppHost.OnStartupException(ex);
            }
        }

#if !NETSTANDARD1_6
        // Entry point for ASP.NET
        public IHttpHandler GetHandler(HttpContext ctx, string requestType, string url, string pathTranslated)
        {
            var context = ctx.Request.RequestContext.HttpContext;
            var appHost = HostContext.AppHost;

            DebugLastHandlerArgs = requestType + "|" + url + "|" + pathTranslated;
            //var httpReq = new AspNetRequest(context, url);
            var httpReq = new ServiceStack.Host.AspNet.AspNetRequest(context, url.SanitizedVirtualPath());
            foreach (var rawHttpHandler in appHost.RawHttpHandlers)
            {
                var reqInfo = rawHttpHandler(httpReq);
                if (reqInfo != null) return reqInfo;
            }

            var mode = appHost.Config.HandlerFactoryPath;
            var pathInfo = context.Request.GetPathInfo();

            //WebDev Server auto requests '/default.aspx' so recorrect path to different default document
            if (mode == null && (url == "/default.aspx" || url == "/Default.aspx"))
                pathInfo = "/";

            //Default Request /
            if (string.IsNullOrEmpty(pathInfo) || pathInfo == "/")
            {
                //If the fallback route can handle it, let it
                if (appHost.Config.FallbackRestPath != null)
                {
                    string contentType;
                    var sanitizedPath = RestHandler.GetSanitizedPathInfo(pathInfo, out contentType);

                    var restPath = appHost.Config.FallbackRestPath(context.Request.HttpMethod, sanitizedPath, pathTranslated);
                    if (restPath != null)
                    {
                        return new RestHandler { RestPath = restPath, RequestName = restPath.RequestType.GetOperationName(), ResponseContentType = contentType };
                    }
                }

                //Exception calling context.Request.Url on Apache+mod_mono
                var absoluteUrl = Env.IsMono ? url.ToParentPath() : context.Request.GetApplicationUrl();
                SetApplicationBaseUrl(absoluteUrl);

                //e.g. CatchAllHandler to Process Markdown files
                var catchAllHandler = GetCatchAllHandlerIfAny(httpReq.HttpMethod, pathInfo, httpReq.GetPhysicalPath());
                if (catchAllHandler != null) return catchAllHandler;

                if (mode == null)
                    return DefaultHttpHandler;

                if (DefaultRootFileName != null)
                    return StaticFilesHandler;

                return NonRootModeDefaultHttpHandler;
            }

            if (mode != null && pathInfo.EndsWith("/" + mode))
            {
                return ReturnDefaultHandler(httpReq);
            }

            return GetHandlerForPathInfo(
                httpReq.HttpMethod, pathInfo, context.Request.FilePath, pathTranslated)
                   ?? NotFoundHttpHandler;
        }
#endif
        private static void SetApplicationBaseUrl(string absoluteUrl)
        {
            if (absoluteUrl == null) return;

            var defaultRedirectUrl = DefaultHttpHandler as RedirectHttpHandler;
            if (defaultRedirectUrl != null && defaultRedirectUrl.AbsoluteUrl == null)
                defaultRedirectUrl.AbsoluteUrl = absoluteUrl.CombineWith(
                defaultRedirectUrl.RelativeUrl);

            if (NonRootModeDefaultHttpHandler != null && NonRootModeDefaultHttpHandler.AbsoluteUrl == null)
                NonRootModeDefaultHttpHandler.AbsoluteUrl = absoluteUrl.CombineWith(
                NonRootModeDefaultHttpHandler.RelativeUrl);
        }

        public static string GetBaseUrl()
        {
            return HostContext.Config.WebHostUrl;
        }

        // Entry point for HttpListener
        public static IHttpHandler GetHandler(IHttpRequest httpReq)
        {
            var appHost = HostContext.AppHost;

            foreach (var rawHttpHandler in appHost.RawHttpHandlers)
            {
                var reqInfo = rawHttpHandler(httpReq);
                if (reqInfo != null) return reqInfo;
            }

            var mode = appHost.Config.HandlerFactoryPath;
            var pathInfo = httpReq.PathInfo;

            //Default Request /
            if (string.IsNullOrEmpty(pathInfo) || pathInfo == "/")
            {
                //If the fallback route can handle it, let it
                if (appHost.Config.FallbackRestPath != null)
                {
                    string contentType;
                    var sanitizedPath = RestHandler.GetSanitizedPathInfo(pathInfo, out contentType);

                    var restPath = appHost.Config.FallbackRestPath(httpReq.HttpMethod, sanitizedPath, httpReq.GetPhysicalPath());
                    if (restPath != null)
                    {
                        return new RestHandler { RestPath = restPath, RequestName = restPath.RequestType.GetOperationName(), ResponseContentType = contentType };
                    }
                }

                SetApplicationBaseUrl(httpReq.GetPathUrl());

                //e.g. CatchAllHandler to Process Markdown files
                var catchAllHandler = GetCatchAllHandlerIfAny(httpReq.HttpMethod, pathInfo, httpReq.GetPhysicalPath());
                if (catchAllHandler != null) return catchAllHandler;

                if (mode == null)
                    return DefaultHttpHandler;

                if (DefaultRootFileName != null)
                    return StaticFilesHandler;

                return NonRootModeDefaultHttpHandler;
            }

            if (mode != null && pathInfo.EndsWith("/" + mode))
            {
                return ReturnDefaultHandler(httpReq);
            }

            return GetHandlerForPathInfo(httpReq.HttpMethod, pathInfo, pathInfo, httpReq.GetPhysicalPath())
                   ?? NotFoundHttpHandler;
        }

        private static IHttpHandler ReturnDefaultHandler(IHttpRequest httpReq)
        {
            var pathProvider = HostContext.VirtualFileSources;

            var defaultDoc = pathProvider.GetFile(DefaultRootFileName ?? "");
            if (httpReq.GetPhysicalPath() != WebHostPhysicalPath
                || defaultDoc == null)
            {
                return new IndexPageHttpHandler();
            }

            var okToServe = ShouldAllow(httpReq.GetPhysicalPath());
            return okToServe ? DefaultHttpHandler : ForbiddenHttpHandler;
        }

        internal static IHttpHandler ReturnRequestInfo(IHttpRequest httpReq)
        {
            if ((HostContext.DebugMode
                || HostContext.Config.AdminAuthSecret != null)
                && httpReq.QueryString[Keywords.Debug] == Keywords.RequestInfo)
            {
                if (HostContext.DebugMode || HostContext.HasValidAuthSecret(httpReq))
                {
                    var reqInfo = RequestInfoHandler.GetRequestInfo(httpReq);

                    reqInfo.Host = HostContext.Config.DebugHttpListenerHostEnvironment + "_v" + Env.ServiceStackVersion + "_" + HostContext.ServiceName;
                    reqInfo.PathInfo = httpReq.PathInfo;
                    reqInfo.GetPathUrl = httpReq.GetPathUrl();

                    return new RequestInfoHandler { RequestInfo = reqInfo };
                }
            }

            return null;
        }

        // no handler registered 
        // serve the file from the filesystem, restricting to a safelist of extensions
        public static bool ShouldAllow(string filePath)
        {
            var parts = filePath.SplitOnLast('.');
            if (parts.Length == 1 || string.IsNullOrEmpty(parts[1]))
                return false;

            var fileExt = parts[1];
            if (HostContext.Config.AllowFileExtensions.Contains(fileExt))
                return true;

            foreach (var pathGlob in HostContext.Config.AllowFilePaths)
            {
                if (filePath.GlobPath(pathGlob))
                    return true;
            }
            return false;
        }

        public static IHttpHandler GetHandlerForPathInfo(string httpMethod, string pathInfo, string requestPath, string filePath)
        {
            var appHost = HostContext.AppHost;

            var firstPathPart = RestPath.GetFirstPathPart(pathInfo);
            if (firstPathPart == null) return NotFoundHttpHandler;

            string contentType;
            var restPath = RestHandler.FindMatchingRestPath(httpMethod, pathInfo, out contentType);
            if (restPath != null)
                return new RestHandler { RestPath = restPath, RequestName = restPath.RequestType.GetOperationName(), ResponseContentType = contentType };

            if (WebHostRootFileNames.Contains(firstPathPart))
            {
                var fileExt = System.IO.Path.GetExtension(filePath);
                var isFileRequest = !string.IsNullOrEmpty(fileExt);

                if (!isFileRequest && !HostAutoRedirectsDirs)
                {
                    //If pathInfo is for Directory try again with redirect including '/' suffix
                    if (!pathInfo.EndsWith("/", StringComparison.Ordinal))
                    {
                        var appFilePath = filePath.Substring(0, filePath.Length - requestPath.Length);
                        var redirect = Host.Handlers.StaticFileHandler.DirectoryExists(filePath, appFilePath);
                        if (redirect)
                        {
                            return new RedirectHttpHandler
                            {
                                RelativeUrl = pathInfo + "/",
                            };
                        }
                    }
                }

                //e.g. CatchAllHandler to Process Markdown files
                var catchAllHandler = GetCatchAllHandlerIfAny(httpMethod, pathInfo, filePath);
                if (catchAllHandler != null) return catchAllHandler;

                if (!isFileRequest)
                {
                    return appHost.VirtualFileSources.DirectoryExists(pathInfo)
                        ? StaticFilesHandler
                        : NotFoundHttpHandler;
                }

                return ShouldAllow(requestPath)
                    ? StaticFilesHandler
                    : ForbiddenHttpHandler;
            }

            var handler = GetCatchAllHandlerIfAny(httpMethod, pathInfo, filePath);
            if (handler != null) return handler;

            if (appHost.Config.FallbackRestPath != null)
            {
                restPath = appHost.Config.FallbackRestPath(httpMethod, pathInfo, filePath);
                if (restPath != null)
                {
                    return new RestHandler { RestPath = restPath, RequestName = restPath.RequestType.GetOperationName(), ResponseContentType = contentType };
                }
            }

            return null;
        }

        private static IHttpHandler GetCatchAllHandlerIfAny(string httpMethod, string pathInfo, string filePath)
        {
            foreach (var httpHandlerResolver in HostContext.CatchAllHandlers)
            {
                var httpHandler = httpHandlerResolver(httpMethod, pathInfo, filePath);
                if (httpHandler != null)
                    return httpHandler;
            }

            return null;
        }

        public void ReleaseHandler(IHttpHandler handler)
        {
        }
    }
}