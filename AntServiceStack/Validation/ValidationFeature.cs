﻿using System;
using System.Linq;
using System.Reflection;
using Funq;
using AntServiceStack.FluentValidation;
using AntServiceStack.FluentValidation.Results;
using Freeway.Logging;
using AntServiceStack.ServiceHost;
using AntServiceStack.WebHost.Endpoints;
using AntServiceStack.Text;

namespace AntServiceStack.Validation
{
    public class ValidationFeature : IPlugin
    {
        public Func<ValidationResult, object, object> ErrorResponseFilter { get; set; }

        /// <summary>
        /// Activate the validation mechanism, so every request DTO with an existing validator
        /// will be validated.
        /// </summary>
        /// <param name="appHost">The app host</param>
        public void Register(IAppHost appHost)
        {
            if(!appHost.RequestFilters.Contains(ValidationFilters.RequestFilter))
                appHost.RequestFilters.Add(ValidationFilters.RequestFilter);
        }
       
        /// <summary>
        /// Override to provide additional/less context about the Service Exception. 
        /// By default the request is serialized and appended to the ResponseStatus StackTrace.
        /// </summary>
        public virtual string GetRequestErrorBody(object request)
        {
            var requestString = "";
            try
            {
                requestString = TypeSerializer.SerializeToString(request);
            }
            catch /*(Exception ignoreSerializationException)*/
            {
                //Serializing request successfully is not critical and only provides added error info
            }

            return string.Format("[{0}: {1}]:\n[REQUEST: {2}]", GetType().Name, DateTime.UtcNow, requestString);
        }
    }

    public static class ValidationExtensions
    {
        /// <summary>
        /// Auto-scans the provided assemblies for a <see cref="IValidator"/>
        /// and registers it in the provided IoC container.
        /// </summary>
        /// <param name="container">The IoC container</param>
        /// <param name="assemblies">The assemblies to scan for a validator</param>
        public static void RegisterValidators(this Container container, params Assembly[] assemblies)
        {
            RegisterValidators(container, ReuseScope.Container, assemblies);
        }

        public static void RegisterValidators(this Container container, ReuseScope scope, params Assembly[] assemblies)
        {
            assemblies = assemblies.Distinct().ToArray();
            foreach (var assembly in assemblies)
            {
                foreach (var validator in assembly.GetTypes()
                .Where(t => t.IsOrHasGenericInterfaceTypeOf(typeof(IValidator<>))))
                {
                    RegisterValidator(container, validator, scope);
                }
            }
        }

        public static void RegisterValidator(this Container container, Type validator, ReuseScope scope=ReuseScope.None)
        {
            var baseType = validator.BaseType;
            while (!baseType.IsGenericType)
            {
                baseType = baseType.BaseType;
            }

            var dtoType = baseType.GetGenericArguments()[0];
            var validatorType = typeof(IValidator<>).MakeGenericType(dtoType);

            container.RegisterAutoWiredType(validator, validatorType, scope);
        }
    }
}
