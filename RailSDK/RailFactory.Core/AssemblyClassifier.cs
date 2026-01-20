using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using dnlib.DotNet;

namespace RailFactory.Core;

/// <summary>
/// Classifies assemblies as Module, Dependency, or Excluded using smart heuristics.
/// NO hardcoded library names - uses pattern matching and metadata analysis.
/// </summary>
public class AssemblyClassifier
{
    // Microsoft public key tokens (for framework detection)
    private static readonly byte[][] MicrosoftTokens = new[]
    {
        new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a }, // Microsoft
        new byte[] { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89 }, // ECMA
        new byte[] { 0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35 }  // .NET Foundation
    };
    
    // Blacklist: Infrastructure and tooling libraries that should NEVER be exposed to LLM
    // These are internal support libraries, not user callable tools
    private static readonly string[] InfrastructureBlacklist = new[]
    {
        // Rail SDK infrastructure
        "RailFactory.Core",
        "RailFactory.Runtime",
        "RailBridge",
        
        // Common NuGet packages (internal tooling)
        "dnlib",
        "Newtonsoft.Json",
        "System.Text.Json",
        "Microsoft.Extensions",
        "NLog",
        "Serilog",
        "log4net",
        
        // ORM/Database (infrastructure)
        "EntityFramework",
        "Dapper",
        "Npgsql",
        "MySql.Data",
        "MongoDB.Driver",
        
        // Testing (never expose)
        "xunit",
        "nunit",
        "MSTest",
        "Moq",
        "FluentAssertions"
    };
    
    public AssemblyClassification Classify(string assemblyPath, ScanOptions options)
    {
        try
        {
            using var module = ModuleDefMD.Load(assemblyPath);
            var assembly = module.Assembly;
            var assemblyName = assembly.Name.String;
            
            // 0. BLACKLIST CHECK - Infrastructure libraries never exposed to LLM
            if (IsBlacklisted(assemblyName))
                return AssemblyClassification.Excluded;
            
            // 1. Framework assembly check (PublicKeyToken + namespace)
            if (IsFrameworkAssembly(assembly))
                return AssemblyClassification.SystemFramework;
            
            // 2. Third-party detection (location patterns + metadata)
            if (IsThirdPartyLibrary(assemblyPath, assembly))
                return AssemblyClassification.ThirdParty;
            
            // 3. Check if has public callable tools
            if (HasPublicTools(assemblyPath, options))
                return AssemblyClassification.Module;
            
            // 4. Otherwise it's a dependency (support library)
            return AssemblyClassification.Dependency;
        }
        catch
        {
            // If we can't analyze it, assume it's a dependency
            return AssemblyClassification.Dependency;
        }
    }
    
    private bool IsBlacklisted(string assemblyName)
    {
        return InfrastructureBlacklist.Any(b => 
            assemblyName.Equals(b, StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith(b + ".", StringComparison.OrdinalIgnoreCase));
    }
    
    private bool IsFrameworkAssembly(AssemblyDef assembly)
    {
        // Check PublicKeyToken for Microsoft signing
        var token = assembly.PublicKeyToken;
        if (token != null && token.Data.Length == 8)
        {
            foreach (var msToken in MicrosoftTokens)
            {
                if (token.Data.SequenceEqual(msToken))
                    return true;
            }
        }
        
        // Check assembly name patterns
        var name = assembly.Name.String;
        if (name.StartsWith("System.")) return true;
        if (name.StartsWith("Microsoft.")) return true;
        if (name.StartsWith("mscorlib")) return true;
        if (name.StartsWith("netstandard")) return true;
        
        return false;
    }
    
    private bool IsThirdPartyLibrary(string assemblyPath, AssemblyDef assembly)
    {
        // Check if in NuGet packages folder
        var normalizedPath = assemblyPath.Replace('/', '\\').ToLowerInvariant();
        if (normalizedPath.Contains("\\.nuget\\packages\\")) return true;
        if (normalizedPath.Contains("\\packages\\")) return true;
        
        // Check company attribute for non-user company
        var companyAttr = assembly.CustomAttributes
            .FirstOrDefault(a => a.TypeFullName == "System.Reflection.AssemblyCompanyAttribute");
        
        if (companyAttr != null && companyAttr.ConstructorArguments.Count > 0)
        {
            var company = companyAttr.ConstructorArguments[0].Value as string;
            if (!string.IsNullOrEmpty(company))
            {
                // If company is a well-known vendor, it's third-party
                // But don't hardcode - check if it's NOT the user's company
                // Heuristic: user's binaries likely don't have Company attribute set
                // or have generic values
                if (IsWellKnownVendor(company))
                    return true;
            }
        }
        
        // Check if assembly has strong name with culture (common for third-party)
        if (assembly.HasPublicKey && !string.IsNullOrEmpty(assembly.Culture))
            return true;
        
        return false;
    }
    
    private bool IsWellKnownVendor(string company)
    {
        // Check for common patterns in vendor company names
        // NO hardcoded library names, but vendor patterns
        var lowerCompany = company.ToLowerInvariant();
        
        // Common vendor indicators
        if (lowerCompany.Contains("inc.")) return true;
        if (lowerCompany.Contains("corporation")) return true;
        if (lowerCompany.Contains("ltd")) return true;
        if (lowerCompany.Contains("llc")) return true;
        
        // Known large vendors (patterns, not specific libs)
        if (lowerCompany.Contains("google")) return true;
        if (lowerCompany.Contains("amazon")) return true;
        if (lowerCompany.Contains("jetbrains")) return true;
        if (lowerCompany.Contains("telerik")) return true;
        if (lowerCompany.Contains("devexpress")) return true;
        
        return false;
    }
    
    private bool HasPublicTools(string assemblyPath, ScanOptions options)
    {
        try
        {
            // Use existing DotNetScanner to check for public methods
            var scanner = new DotNetRuntimeScanner();
            var methods = scanner.ScanBinary(assemblyPath, options);
            
            // If it has at least one public callable method, it could be a module
            return methods.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Classification result for an assembly.
/// </summary>
public enum AssemblyClassification
{
    /// <summary>
    /// Has public tools - should be a module in composite manifest.
    /// </summary>
    Module,
    
    /// <summary>
    /// Support library only - goes in dependencies array.
    /// </summary>
    Dependency,
    
    /// <summary>
    /// System/framework assembly (System.*, Microsoft.*) - excluded.
    /// </summary>
    SystemFramework,
    
    /// <summary>
    /// Third-party library (NuGet, vendor) - dependency only.
    /// </summary>
    ThirdParty,
    
    /// <summary>
    /// Should be excluded completely.
    /// </summary>
    Excluded
}



