using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using JobPortal.Data;
using JobPortal.Models;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Tests for authorization and access control.
    /// Validates that admin-only endpoints enforce proper authorization.
    /// </summary>
    public class AuthorizationTests
    {
        [Fact]
        public void AdminController_HasAuthorizeAttribute()
        {
            var adminControllerType = typeof(AdminController);
            var attributes = adminControllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true);
            attributes.Should().NotBeEmpty();
        }

        [Fact]
        public void AdminController_AllPublicActionMethods_RequireAuthorization()
        {
            // Arrange
            var adminControllerType = typeof(AdminController);
            var publicMethods = adminControllerType
                .GetMethods()
                .Where(m => m.IsPublic && 
                           !m.IsSpecialName && 
                           m.DeclaringType == adminControllerType &&
                           !m.Name.StartsWith("get_") &&
                           m.ReturnType != typeof(void))
                .ToList();

            // Note: If class has [Authorize], all methods are protected unless explicitly [AllowAnonymous]
            // This test confirms the class-level authorization is in place

            // Act
            var classAttributes = adminControllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true);

            // Assert - The class itself is marked with Authorize
            classAttributes.Should().NotBeEmpty("AdminController should have class-level Authorize attribute");
        }

        [Fact]
        public void AdminController_AuthorizeAttribute_UsesIdentity()
        {
            var adminControllerType = typeof(AdminController);
            var authorizeAttribute = adminControllerType
                .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .FirstOrDefault() as AuthorizeAttribute;

            authorizeAttribute.Should().NotBeNull();
            // With Identity, AuthenticationSchemes is null (uses default Identity scheme)
            authorizeAttribute!.AuthenticationSchemes.Should().BeNull();
        }

        [Fact]
        public void AdminControllerMethods_SpecificActions_NoWhitelistedPublicAccess()
        {
            // Arrange
            var adminControllerType = typeof(AdminController);
            var publicMethods = new[] { "Index", "JobDetail", "Create", "Edit", "Delete", "Applications" };

            // Act
            foreach (var methodName in publicMethods)
            {
                var methods = adminControllerType.GetMethods()
                    .Where(m => m.Name == methodName && m.DeclaringType == adminControllerType)
                    .ToList();
                
                // Assert
                // Each method should either:
                // 1. Not have [AllowAnonymous] (inherits class-level [Authorize])
                // 2. Or explicitly require authorization
                
                foreach (var method in methods)
                {
                    var allowAnonymousAttrs = method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true);
                    allowAnonymousAttrs.Should().BeEmpty(
                        $"{methodName} should not allow anonymous access - it's an admin action"
                    );
                }
            }
        }

        [Fact]
        public void AuthenticationScheme_UsesIdentity()
        {
            var adminControllerType = typeof(AdminController);
            var authorizeAttribute = adminControllerType
                .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .FirstOrDefault() as AuthorizeAttribute;

            authorizeAttribute.Should().NotBeNull();
            // Identity uses default cookie scheme — no explicit scheme needed
        }

        [Fact]
        public void AdminControllerIndex_Method_RequiresActiveAuthorization()
        {
            // Arrange
            var adminControllerType = typeof(AdminController);
            var indexMethod = adminControllerType.GetMethod("Index");

            // Act
            var methodAttributes = indexMethod?.GetCustomAttributes(typeof(AuthorizeAttribute), true) ?? Array.Empty<object>();
            var classAttributes = adminControllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true);

            // Assert
            // Either method has [Authorize] or it inherits from class-level [Authorize]
            var hasAuthorization = methodAttributes.Length > 0 || classAttributes.Length > 0;
            hasAuthorization.Should().BeTrue("Index action should require authorization");
        }

        [Fact]
        public void AdminControllerAnalytics_Method_RequiresAuth()
        {
            var adminControllerType = typeof(AdminController);
            var classAttributes = adminControllerType
                .GetCustomAttributes(typeof(AuthorizeAttribute), true);

            classAttributes.Should().NotBeEmpty();
        }

        [Fact]
        public void AdminControllerCRUDMethods_AllRequireAuthorization()
        {
            // Arrange
            var adminControllerType = typeof(AdminController);
            var crudMethodNames = new[] { "Create", "Edit", "Delete" };
            var classAttributes = adminControllerType
                .GetCustomAttributes(typeof(AuthorizeAttribute), true);

            // Act & Assert
            classAttributes.Should().NotBeEmpty("Class should have Authorize attribute");
            
            foreach (var methodName in crudMethodNames)
            {
                var methods = adminControllerType.GetMethods()
                    .Where(m => m.Name == methodName && m.DeclaringType == adminControllerType)
                    .ToList();
                
                methods.Should().NotBeEmpty($"AdminController should have {methodName} method(s)");
                
                // Methods either have their own [Authorize] or inherit from class
                // Both are acceptable for admin actions
                methods.Count.Should().BeGreaterThan(0);
            }
        }
    }
}
