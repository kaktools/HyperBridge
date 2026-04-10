using HyperBridge.Core.Enums;
using HyperBridge.Core.Models;

namespace HyperBridge.Services;

public static class CompatibilityAdvisor
{
    public static CompatibilityAssessment Evaluate(VirtualMachineAnalysis analysis)
    {
        var assessment = new CompatibilityAssessment
        {
            Level = CompatibilityLevel.Green,
            Recommendation = "Gen2 empfohlen",
            SuggestedGeneration = 2,
        };

        var os = analysis.Summary.GuestOsType.ToLowerInvariant();
        var state = analysis.Summary.State.ToLowerInvariant();
        var diskType = analysis.DiskType.ToUpperInvariant();
        var riskScore = 0;

        if (os.Contains("windows10", StringComparison.Ordinal)
            || os.Contains("windows11", StringComparison.Ordinal)
            || os.Contains("windows2016", StringComparison.Ordinal)
            || os.Contains("windows2019", StringComparison.Ordinal)
            || os.Contains("windows2022", StringComparison.Ordinal)
            || os.Contains("ubuntu", StringComparison.Ordinal)
            || os.Contains("debian", StringComparison.Ordinal)
            || os.Contains("centos", StringComparison.Ordinal))
        {
            assessment.Reasons.Add("Modernes Gast-OS erkannt, Gen2 ist typischerweise sinnvoll.");
        }
        else
        {
            riskScore += 2;
            assessment.Reasons.Add("Gast-OS ist alt oder nicht eindeutig zuordenbar.");
        }

        if (analysis.HasSnapshots)
        {
            riskScore += 1;
            assessment.Reasons.Add("Snapshots vorhanden: erhöhte Komplexität.");
        }

        if (analysis.IsRunning || state == "saved")
        {
            riskScore += 3;
            assessment.Reasons.Add("VM ist nicht in sauberem ausgeschaltetem Zustand.");
        }

        if (diskType is "VDI" or "VMDK")
        {
            assessment.Reasons.Add($"Datenträgerformat {diskType} ist migrierbar.");
        }
        else
        {
            riskScore += 2;
            assessment.Reasons.Add($"Datenträgerformat {diskType} ist ungewöhnlich.");
        }

        if (os.Contains("xp", StringComparison.Ordinal)
            || os.Contains("2003", StringComparison.Ordinal)
            || os.Contains("2008", StringComparison.Ordinal)
            || os.Contains("32", StringComparison.Ordinal))
        {
            riskScore += 3;
            assessment.Reasons.Add("Altes oder 32-Bit-OS erkannt: Gen1 wahrscheinlicher kompatibel.");
        }

        if (riskScore <= 1)
        {
            assessment.Actions.Add("Gen2 als Standard verwenden.");
            assessment.Actions.Add("Bei Bootproblemen Gen1 als Gegentest nutzen.");
            return assessment;
        }

        if (riskScore <= 4)
        {
            var yellow = new CompatibilityAssessment
            {
                Level = CompatibilityLevel.Yellow,
                Recommendation = "Gen2 möglich, aber prüfen",
                SuggestedGeneration = 2,
                Actions =
                {
                    "Migration mit Gen2 starten.",
                    "Bei Startproblemen Secure Boot deaktivieren oder auf Gen1 wechseln.",
                },
            };
            yellow.Reasons.AddRange(assessment.Reasons);
            return yellow;
        }

        if (riskScore <= 6)
        {
            var gen1 = new CompatibilityAssessment
            {
                Level = CompatibilityLevel.Yellow,
                Recommendation = "Gen1 empfohlen",
                SuggestedGeneration = 1,
                Actions =
                {
                    "Starte die erste Ziel-VM mit Gen1.",
                    "Dokumentiere BIOS-/Boot-Hinweise und teste danach optional Gen2.",
                },
            };
            gen1.Reasons.AddRange(assessment.Reasons);
            return gen1;
        }

        var risky = new CompatibilityAssessment
        {
            Level = CompatibilityLevel.Red,
            Recommendation = "Migration riskant",
            SuggestedGeneration = 1,
            Actions =
            {
                "Vor Migration vollständiges Backup erstellen.",
                "Zuerst Dry Run ausführen und Hinweise prüfen.",
                "Bei kritischen Altsystemen Gen1 bevorzugen.",
            },
        };
        risky.Reasons.AddRange(assessment.Reasons);
        return risky;
    }
}