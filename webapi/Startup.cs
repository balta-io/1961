using AuthBearer.Models;
using Microsoft.AspNet.Authentication.JwtBearer;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using System;
using System.IdentityModel.Tokens;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AuthBearer
{
    public class Startup
    {
        const string TokenAudience = "ExampleAudience";
        const string TokenIssuer = "ExampleIssuer";

        private RsaSecurityKey _key;
        private TokenAuthOptions _tokenOptions;
        private readonly IApplicationEnvironment _environment;

        public Startup(IApplicationEnvironment environment)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables();
            Configuration = builder.Build();
            _environment = environment;
        }

        public RSAParameters GetRSAParameters()
        {
            var cert = new X509Certificate2(Path.Combine(_environment.ApplicationBasePath, "idsrv4test.pfx"), "idsrv3test", X509KeyStorageFlags.Exportable);
            using (RSACryptoServiceProvider rsa = (RSACryptoServiceProvider)cert.PrivateKey)
            {
                try
                {
                    return rsa.ExportParameters(true);
                }
                finally
                {
                    rsa.PersistKeyInCsp = false;
                }
            }
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Obtem os par�metros para gerar o token com base na chave privada do certificado digital
            var rsaParameters = GetRSAParameters();

            //Criar uma chave para compor o token de autentica��o
            _key = new RsaSecurityKey(rsaParameters);

            // Cria as informa��es que estaram no token
            _tokenOptions = new TokenAuthOptions
            {
                // Aplica��o que est� solicitando o token
                Audience = TokenAudience,

                // Aplica��o que est� gerando o token
                Issuer = TokenIssuer,

                // Credencias de entrada
                SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256Signature)
            };

            // Registro da classe TokenAuthOptions para injetar a dep�ndencia no controller que ir� fazer a autentica��o
            services.AddInstance<TokenAuthOptions>(_tokenOptions);


            // Adicionando o MVC e configura��o Authorization Police e Authorize Filter
            services.AddMvc(config =>
            {
                var policy = new AuthorizationPolicyBuilder()
                                 .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                                 .RequireAuthenticatedUser()
                                 .Build();
                config.Filters.Add(new AuthorizeFilter(policy));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseIISPlatformHandler();

            app.UseJwtBearerAuthentication(options =>
            {
                // Configura��es b�sicas - Assinando a chave para validar com, audi�ncia e emitente.
                options.TokenValidationParameters.IssuerSigningKey = _key;
                options.TokenValidationParameters.ValidAudience = _tokenOptions.Audience;
                options.TokenValidationParameters.ValidIssuer = _tokenOptions.Issuer;

                // Ao receber um sinal, verificar que temos assinado.
                options.TokenValidationParameters.ValidateSignature = true;

                // Ao receber um sinal, verificar se ainda � v�lido.
                options.TokenValidationParameters.ValidateLifetime = true;

                //Isto define a inclina��o m�xima permitida rel�gio - ou seja, fornece uma toler�ncia sobre o tempo de expira��o do token
                //ao validar o tempo de vida. Como estamos criando os s�mbolos localmente e validando-os na mesma
                //m�quinas que deveria ter sincronizados tempo, isso pode ser definido para zero.Onde fichas externas s�o
                //usado, alguma margem de manobra aqui poderia ser �til.
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(0);
            });

            app.UseStaticFiles();

            app.UseMvc();
        }

        public static void Main(string[] args) => Microsoft.AspNet.Hosting.WebApplication.Run<Startup>(args);
    }
}
