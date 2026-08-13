using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.FormulaRepository.Model;

internal sealed class FormulaDefinition(FormulaDefinitionDto dto)
{
    public FormulaDefinitionDto Definition { get; }=dto;
    public List<FormulaVersion> Versions { get; }=[];
}
internal sealed class FormulaVersion(FormulaVersionDto dto)
{
    public FormulaVersionDto Version { get; set; }=dto;
    public List<FormulaDependencyDto> Dependencies { get; }=[];
    public List<FormulaTestCaseDto> TestCases { get; }=[];
}
internal sealed class ParameterSetVersion(ParameterSetVersionDto dto) { public ParameterSetVersionDto Version { get; set; }=dto; public List<ParameterValueDto> Values { get; }=[]; }
internal sealed class LookupTableVersion(LookupTableVersionDto dto) { public LookupTableVersionDto Version { get; set; }=dto; public List<LookupRowDto> Rows { get; }=[]; }
internal sealed class RuleSetVersion(RuleSetVersionDto dto) { public RuleSetVersionDto Version { get; set; }=dto; public List<RuleDto> Rules { get; }=[]; }

public sealed class FormulaRepositoryValidationException(PayCalc24.Contracts.Diagnostics.Diagnostic diagnostic):Exception(diagnostic.Code) { public PayCalc24.Contracts.Diagnostics.Diagnostic Diagnostic { get; }=diagnostic; }
