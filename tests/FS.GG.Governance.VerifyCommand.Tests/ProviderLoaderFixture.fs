namespace FS.GG.Governance.VerifyCommand.Tests

// Synthetic codec/admission controls against retained package APIs; no provider execution.
open System
open System.Text
open Fsgg
open Fsgg.ProviderCatalog
open FS.GG.SDD.Commands
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Artifacts.CatalogScaffoldProvenance
open FS.GG.Governance.Config

module ProviderLoaderFixture =
    let digest (bytes: byte array) = ProviderCatalogIntegrity.digest bytes
    let parameter key : ProviderCatalog.Parameter =
        {
            Key = key
            Kind = ProviderCatalog.String
            Required = true
            Prompt = key
            Help = key
            Default = Some key
            Values = []
            Validation =
                {
                    NonEmpty = true
                    MinLength = 1
                    MaxLength = 100
                    AllowedValues = []
                }
        }


    let command: Provider.DeclaredCommand =
        {
            Executable = "opaque-tool"
            Arguments = [ ""; "with spaces"; "$(literal);&" ]
        }

    let descriptor: ProviderCatalog.Descriptor =
        {
            Id = "generic-fixture"
            DisplayName = "Fixture"
            Help = "Fixture"
            Language = "opaque"
            ProductShape = "fixture"
            DescriptorId = "fixture-descriptor"
            DescriptorRevision = "revision"
            DescriptorDigest = digest [||]
            ContractVersion = "3.0.0"
            TemplateSource = "opaque-source"
            TemplateId = "opaque-template"
            Platforms = [ "fixture-platform" ]
            Parameters = [ parameter "raw"; parameter "package"; parameter "code" ]
            Identities =
                {
                    RawName = "raw"
                    PackageIdentity = "package"
                    CodeIdentifier = "code"
                }
            Tools =
                [
                    {
                        Id = "opaque-tool"
                        Version = "1.2.3"
                        Platforms = [ "fixture-platform" ]
                    }
                ]
            Capabilities =
                [
                    {
                        Id = "build:build"
                        Required = true
                        Platforms = [ "fixture-platform" ]
                        ToolIds = [ "opaque-tool" ]
                        EvidenceIds = [ "build-output" ]
                        Binding =
                            ProviderCatalog.Command(
                                command,
                                {
                                    WorkingDirectory = "."
                                    TimeoutSeconds = 2
                                    CostClass = "cheap"
                                    EnvironmentIds = [ "fixture-local" ]
                                }
                            )
                    }
                ]
            Evidence =
                [
                    {
                        Id = "build-output"
                        Format = "opaque@literal"
                        Path = "out/build.json"
                        Required = true
                    }
                ]
            Skills = []
        }

    let reseal (d: ProviderCatalog.Descriptor) = { d with DescriptorDigest = digest (ProviderCatalogIntegrity.descriptorBytes d) }

    let sealedCatalog () : ProviderCatalog.Catalog =
        let d = { descriptor with DescriptorDigest = digest (ProviderCatalogIntegrity.descriptorBytes descriptor) }
        let c = { SchemaVersion = 2; Id = "synthetic-catalog"; Revision = "qualification"; Digest = digest [||]; Providers = [d] }
        { c with Digest = digest (ProviderCatalogIntegrity.catalogBytes c) }

    let synthetic () =
        let catalog = sealedCatalog ()
        let d = catalog.Providers.Head

        let prepared =
            match resolve catalog d.Id [ "raw", "Awkward name!?" ] with
            | Ok p -> p
            | Error e -> failwithf "%A" e

        let hash = "sha256:" + System.String('a', 64)

        let generator: FS.GG.SDD.Artifacts.SchemaVersion.GeneratorVersion =
            {
                Id = "synthetic-generator"
                Version = "1.0.0"
            }

        let path owner name =
            {
                Path = name
                Owner = owner
                Sha256 = Some hash
            }

        let ownership: ScaffoldProvenanceRecord =
            {
                SchemaVersion = 1
                Generator = generator
                RequiredMinimumCliVersion = None
                ProviderName = d.Id
                ProviderContractVersion = d.ContractVersion
                TemplateRef = d.TemplateSource
                Outcome = "providerSucceeded"
                ProducedPaths = [ path GeneratedProduct "src/product.txt" ]
                MirroredPaths = [ path Mirrored ".codex/skills/product/SKILL.md" ]
                SddOwnedPaths = [ path Sdd ".fsgg/project.yml" ]
                DriverPaths = [ path Driver ".agents/skills/driver/SKILL.md" ]
                GameSkillPaths = [ path GameSkill ".agents/skills/game/SKILL.md" ]
                RenderingSkillPaths = [ path RenderingSkill ".agents/skills/render/SKILL.md" ]
                EffectiveParameters = prepared.EffectiveParameters
            }

        let observation: Observation =
            {
                Platform = "fixture-platform"
                Tools =
                    [
                        {
                            Id = "opaque-tool"
                            Version = "1.2.3"
                            Executable = "opaque-tool"
                        }
                    ]
                ConsumedArchiveDigest = hash
                Transport =
                    {
                        Executable = "dotnet"
                        Version = "10.0.401"
                    }
                Invocations =
                    [
                        {
                            Executable = "dotnet"
                            Arguments = [ "new"; "literal space"; ""; "$(literal)" ]
                            WorkingRoot = "staging"
                            WorkingDirectory = "."
                            EnvironmentRoots = []
                            TimeoutSeconds = 30
                            ExitCode = 0
                        }
                    ]
                Result = "succeeded"
                ProducedPaths =
                    ownership.ProducedPaths
                    @ ownership.MirroredPaths
                    @ ownership.SddOwnedPaths
                    @ ownership.DriverPaths
                    @ ownership.GameSkillPaths
                    @ ownership.RenderingSkillPaths
            }

        {
            SchemaVersion = 2
            Generator = generator
            Ownership = ownership
            Observation = observation
            Declaration =
                {
                    RawCatalogDigest = hash
                    CatalogId = catalog.Id
                    CatalogRevision = catalog.Revision
                    CatalogDigest = catalog.Digest
                    Descriptor = d
                    EffectiveParameters = prepared.EffectiveParameters
                    RawProductName = prepared.RawProductName
                    PackageIdentity = prepared.PackageIdentity
                    CodeIdentifier = prepared.CodeIdentifier
                    Archive =
                        {
                            Id = "synthetic-package"
                            Version = "1.0.0"
                            Digest = hash
                        }
                    Policy =
                        {
                            Id = "synthetic-policy"
                            Version = "1.0.0"
                            Digest = hash
                        }
                    EvidenceMap =
                        {
                            Id = "synthetic-map"
                            Version = "1.0.0"
                            Digest = hash
                        }
                    Budgets =
                        {
                            RequestedPreflightSeconds = 10
                            RequestedScaffoldSeconds = 30
                            AdmittedPreflightSeconds = 10
                            AdmittedScaffoldSeconds = 30
                        }
                    Admission =
                        {
                            ContractVersion = "1.0.0"
                            Platform = "fixture-platform"
                            RequiredCapabilityIds = []
                            SemanticOnlyCapabilityIds = []
                            SupportedPlatforms = [ "fixture-platform" ]
                            SupportedEnvironmentIds = [ "fixture-local" ]
                            SupportedFormats = [ { Id = "opaque-format"; Version = "1.0.0" } ]
                            EvidenceMappings =
                                [
                                    {
                                        Format = "opaque@literal"
                                        Id = "opaque-format"
                                        Version = "1.0.0"
                                    }
                                ]
                            Descriptor = d
                        }
                }
        }
