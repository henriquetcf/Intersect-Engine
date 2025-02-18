using System;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Routing;

using Intersect.Configuration;
using Intersect.Enums;
using Intersect.Logging;
using Intersect.Server.Localization;
using Intersect.Server.Web.RestApi.Authentication;
using Intersect.Server.Web.RestApi.Authentication.OAuth;
using Intersect.Server.Web.RestApi.Configuration;
using Intersect.Server.Web.RestApi.Constraints;
using Intersect.Server.Web.RestApi.Logging;
using Intersect.Server.Web.RestApi.Middleware;
using Intersect.Server.Web.RestApi.Payloads;
using Intersect.Server.Web.RestApi.RouteProviders;
using Intersect.Server.Web.RestApi.Services;
using Intersect.Server.Web.RestApi.Swagger;

using Microsoft.Owin.Hosting;
using Microsoft.Owin.Logging;

using Owin;

using Swashbuckle.Application;

namespace Intersect.Server.Web.RestApi
{
    // TODO: Migrate to a proper service
    internal sealed partial class RestApi : IAppConfigurationProvider, IConfigurable<ApiConfiguration>, IDisposable
    {
        private readonly object mDisposeLock;

        private IDisposable mWebAppHandle;

        public RestApi(ushort apiPort)
        {
            mDisposeLock = new object();

            StartOptions = new StartOptions();

            Configuration = ApiConfiguration.Create();

            Configuration.Hosts.ToList().ForEach(host => StartOptions.Urls?.Add(host));

            if (apiPort > 0)
            {
                StartOptions.Urls?.Clear();
                StartOptions.Urls?.Add("http://*:" + apiPort + "/");
            }

            AuthenticationProvider = new OAuthProvider(Configuration);
        }

        public bool Disposing { get; private set; }

        public bool Disposed { get; private set; }

        public bool IsStarted => mWebAppHandle != null;

        public StartOptions StartOptions { get; }

        private AuthenticationProvider AuthenticationProvider { get; }

        public void Configure(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            var config = new HttpConfiguration();

            var services = config.Services;
            if (services == null)
            {
                throw new InvalidOperationException();
            }

            appBuilder.Use<NetworkFilterMiddleware>(Configuration.AllowedNetworkTypes);

            Configuration.Cors.Select(configuration => configuration.AsCorsOptions())
                ?.ToList()
                .ForEach(corsOptions => appBuilder.UseCors(corsOptions));

            var constraintResolver = new DefaultInlineConstraintResolver();
            constraintResolver.ConstraintMap?.Add(nameof(AdminActions), typeof(AdminActionsConstraint));
            constraintResolver.ConstraintMap?.Add(nameof(LookupKey), typeof(LookupKey.Constraint));
            constraintResolver.ConstraintMap?.Add(nameof(ChatMessage), typeof(ChatMessage.Constraint));

            // Map routes
            config.MapHttpAttributeRoutes(constraintResolver, new VersionedRouteProvider());
            config.DependencyResolver = new IntersectServiceDependencyResolver(Configuration, config);

            // Make JSON the default response type for browsers
            config.Formatters?.XmlFormatter.RemoveSupportedMediaType("application/xml");
            config.Formatters?.XmlFormatter.RemoveSupportedMediaType("text/xml");
            config.Formatters?.JsonFormatter.RemoveSupportedMediaType("text/json");
            config.Formatters?.JsonFormatter.Map("accept", "text/html", "application/json");

            if (Configuration.DebugMode)
            {
                config.EnableSwagger(swaggerConfig =>
                {
                    swaggerConfig.MultipleApiVersions(
                        (description, version) => true,
                        versionBuilder => versionBuilder.Version("v1", "Intersect v1 REST API")
                    );

                    swaggerConfig.OperationFilter<AuthorizationFilter>();
                }).EnableSwaggerUi(swaggerUi =>
                {
                    swaggerUi.SupportedSubmitMethods(Array.Empty<string>());
                });

                appBuilder.SetLoggerFactory(new IntersectLoggerFactory());
            }

            if (Configuration.RequestLogging)
            {
                appBuilder.Use<IntersectRequestLoggingMiddleware>(Configuration.RequestLogLevel);
            }

            appBuilder.Use<IntersectThrottlingMiddleware>(
                Configuration.ThrottlePolicy, null, Configuration.FallbackClientKey, null
            );

            AuthenticationProvider.Configure(appBuilder);

            appBuilder.UseWebApi(config);
        }

        public ApiConfiguration Configuration { get; }

        public void Dispose()
        {
            lock (mDisposeLock)
            {
                if (Disposed || Disposing)
                {
                    return;
                }

                Disposing = true;
            }

            mWebAppHandle?.Dispose();
            Disposed = true;
        }

        public void Start()
        {
            if (!Configuration.Enabled)
            {
                return;
            }

            try
            {
                mWebAppHandle = WebApp.Start(StartOptions, Configure);
                System.Diagnostics.Trace.Listeners.Remove("HostingTraceListener");
                StartOptions.Urls?.ToList().ForEach(host => Console.WriteLine(Strings.Intro.api.ToString(host)));
            }
            catch (Exception exception)
            {
                Console.WriteLine(Strings.Intro.apifailed);
                Log.Error(Strings.Intro.apifailed + Environment.NewLine + exception);
            }
        }

    }

}
