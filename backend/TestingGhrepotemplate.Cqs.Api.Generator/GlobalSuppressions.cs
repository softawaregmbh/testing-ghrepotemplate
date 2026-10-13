// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035:Do not use APIs banned for analyzers", Justification = "False positive in Scriban (see https://github.com/dotnet/roslyn-analyzers/issues/6467)", Scope = "member", Target = "~M:Scriban.TemplateContext.#ctor(Scriban.Runtime.ScriptObject,System.Collections.Generic.IEqualityComparer{System.String})")]
