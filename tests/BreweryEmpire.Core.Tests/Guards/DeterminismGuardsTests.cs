using System;
using System.Linq;
using System.Reflection;
using BreweryEmpire.Core.Economy;
using FluentAssertions;
using Xunit;

namespace BreweryEmpire.Core.Tests.Guards
{
    /// <summary>
    /// Compile-time / reflection guards that keep the Phase 1 core honest.
    ///
    /// Determinism is the whole point of building the engine before the Unity
    /// front end. These tests fail loudly if someone sneaks in a source of
    /// platform-dependent behaviour, so a regression cannot slip through in
    /// code review.
    /// </summary>
    public class DeterminismGuardsTests
    {
        private static readonly Assembly Core =
            typeof(Money).Assembly;

        [Fact]
        public void Core_Never_Uses_System_Random()
        {
            var offenders = Core.GetTypes()
                .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static))
                .Select(m => m?.ToString())
                .Where(text => text != null && text.Contains("System.Random"))
                .ToList();

            offenders.Should().BeEmpty(
                "System.Random is platform-seeded and would break replay");
        }

        [Fact]
        public void Core_Never_Uses_Guid_NewGuid()
        {
            // Entity ids must be deterministic. A Guid.NewGuid in the core would
            // make every save non-reproducible.
            Core.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static))
                .Where(m => !m.IsSpecialName)
                .Should().NotContain(m => m.Name.Contains("NewGuid"),
                    "Guids must never mint entity identities");
        }

        [Fact]
        public void Core_Never_Uses_DateTime_Now_UtcNow_Or_Today()
        {
            // Wall-clock time has no place in a fixed-step simulation.
            var offenders = Core.GetTypes()
                .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static))
                .Select(m => m?.ToString())
                .Where(text => text != null && (text.Contains("DateTime.Now") ||
                                                 text.Contains("DateTime.UtcNow") ||
                                                 text.Contains("DateTime.Today")))
                .ToList();

            offenders.Should().BeEmpty(
                "the simulation clock is GameDate, never the wall clock");
        }

        [Fact]
        public void Core_Never_Uses_Single_Or_Double_Floating_Point_For_State()
        {
            // Float fields would diverge across ARM/x64 in the compiled Unity
            // build. State must be integer or decimal.
            var offenders = Core.GetTypes()
                .Where(t => !t.IsNested)
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Instance | BindingFlags.Static))
                .Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(double))
                .Select(f => f.DeclaringType!.Name + "." + f.Name)
                .ToList();

            offenders.Should().BeEmpty(
                "floating point in state risks cross-platform divergence; offenders: " +
                string.Join(", ", offenders));
        }

        [Fact]
        public void Core_Never_References_Unity_Engine()
        {
            // Phase 1 is a Unity-free logic foundation on purpose.
            Core.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Should().NotContain(name => name != null &&
                                             (name.StartsWith("Unity", StringComparison.OrdinalIgnoreCase) ||
                                              name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase)),
                    "the core must stay free of any Unity dependency");
        }

        [Fact]
        public void All_State_Identifiers_Are_String_Backed_Not_Guid()
        {
            var idTypes = Core.GetTypes()
                .Where(t => t.Name.EndsWith("Id", StringComparison.Ordinal))
                .ToList();

            var offenders = idTypes
                .Where(t => t.GetProperty("Value")?.PropertyType != typeof(string))
                .Select(t => t.Name)
                .ToList();

            offenders.Should().BeEmpty("entity ids expose a stable string so saves stay comparable; offenders: " +
                                       string.Join(", ", offenders));
        }

        [Fact]
        public void Money_Is_Long_Backed_Never_Float()
        {
            var centsProp = typeof(Money).GetProperty("Cents",
                BindingFlags.Public | BindingFlags.Instance);

            centsProp.Should().NotBeNull("Money exposes Cents");
            centsProp!.PropertyType.Should().Be(typeof(long),
                "money is integer cents so balances are exact and identical on every platform");
        }
    }
}
