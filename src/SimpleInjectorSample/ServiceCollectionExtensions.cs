using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Framework.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceProvider UseSimpleInjector(this IServiceCollection services)
        {
            var container = new Container();
            var scopeFactory = new SimpleInjectorServiceScopeFactory(container);

            container.Options.AllowOverridingRegistrations = true;
            container.Options.ResolveUnregisteredCollections = true;
            container.Options.SuppressLifestyleMismatchVerification = true;
            container.Options.DefaultScopedLifestyle = scopeFactory;

            foreach (var descriptor in services)
            {
                //if (descriptor.ServiceType.FullName.IndexOf("IConfigureOptions") >= 0)
                //{
                //    System.Diagnostics.Debugger.Break();
                //}

                var lifetime = ConvertLifetimeToSimpleInjectorLifetime(descriptor.Lifetime);

                if (descriptor.ImplementationType != null)
                {
                    var serviceTypeInfo = descriptor.ServiceType.GetTypeInfo();

                    if (serviceTypeInfo.IsGenericTypeDefinition)
                    {
                        if (descriptor.ImplementationType.GetTypeInfo().IsGenericTypeDefinition)
                        {
                            // How do you pass the lifetime here?
                            container.Register(descriptor.ServiceType, descriptor.ImplementationType, lifetime);
                        }
                        else
                        {
                            container.Register(descriptor.ServiceType, new[] { descriptor.ImplementationType }, lifetime);
                        }
                    }
                    else
                    {
                        container.Register(descriptor.ServiceType, descriptor.ImplementationType, lifetime);

                        // for collection based
                        container.Register(descriptor.ImplementationType, descriptor.ImplementationType, lifetime);
                    }
                }
                else if (descriptor.ImplementationFactory != null)
                {
                    container.Register(
                        descriptor.ServiceType,
                        () => descriptor.ImplementationFactory(container.GetService<IServiceProvider>()),
                        lifetime);
                }
                else
                {
                    container.Register(
                        descriptor.ServiceType,
                        () => descriptor.ImplementationInstance,
                        lifetime);
                }
            }

            var groupedTypes = services
                .GroupBy(s => s.ServiceType);

            foreach (var groupedType in groupedTypes)
            {
                var serviceType = groupedType.Key;
                var type = typeof(IEnumerable<>).MakeGenericType(serviceType);

                if (serviceType.GetTypeInfo().IsGenericTypeDefinition)
                {
                    container.RegisterCollection(serviceType, groupedType.Select(t => t.ImplementationType));
                }
                else
                {
                    // TODO: Get longest lifestyle?
                    var lifetime = ConvertLifetimeToSimpleInjectorLifetime(groupedType.First().Lifetime);

                    container.Register(
                        type,
                        () =>
                        {
                            var collectionServices = groupedType
                                .Where(c => c.ImplementationType != null)
                                .Select(c => container.GetRequiredService(c.ImplementationType));

                            return typeof(System.Linq.Enumerable)
                                    .GetMethod("Cast", new[] { typeof(System.Collections.IEnumerable) })
                                    .MakeGenericMethod(groupedType.Key)
                                    .Invoke(null, new object[] { collectionServices });
                        },
                        lifetime);
                }
            }

            container.Register<IServiceProvider>(() => container, Lifestyle.Singleton);
            container.Register<IServiceScopeFactory>(() => scopeFactory, Lifestyle.Singleton);

            // container.Verify();

            return container;
        }

        private static Lifestyle ConvertLifetimeToSimpleInjectorLifetime(ServiceLifetime lifetime)
        {
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    return Lifestyle.Singleton;

                case ServiceLifetime.Scoped:
                    return Lifestyle.Scoped;

                case ServiceLifetime.Transient:
                    return Lifestyle.Transient;

                default:
                    throw new NotSupportedException();
            }
        }
    }

    public class SimpleInjectorScope : IServiceScope
    {
        private readonly Container _container;
        private readonly Scope _lifetimeScope;

        public SimpleInjectorScope(Scope lifetimeScope, Container container)
        {
            _container = container;
            _lifetimeScope = lifetimeScope;
        }

        public IServiceProvider ServiceProvider
        {
            get
            {
                return _container;
            }
        }

        public void Dispose()
        {
            _lifetimeScope.Dispose();
        }
    }

    public class SimpleInjectorServiceScopeFactory : ScopedLifestyle, IServiceScopeFactory
    {
        private readonly Container _container;

        public SimpleInjectorServiceScopeFactory(IServiceProvider container)
            : base("ASP.NET 5 Scope")
        {
            _container = container as Container;
        }

        public IServiceScope CreateScope()
        {
            return new SimpleInjectorScope(CreateScopeCore(), _container);
        }

        protected override Func<Scope> CreateCurrentScopeProvider(Container container)
        {
            return CreateScopeCore;
        }

        private Scope CreateScopeCore()
        {
            return _container.GetCurrentLifetimeScope() ?? _container.BeginLifetimeScope();
        }
    }
}