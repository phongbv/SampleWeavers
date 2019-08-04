using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class ModuleWeaver
{
    private ReferenceFinder _referenceFinder;
    // Will contain the full element XML from FodyWeavers.xml. OPTIONAL
    public XElement Config { get; set; }

    // Will log an MessageImportance.Normal message to MSBuild. OPTIONAL
    public Action<string> LogDebug { get; set; }

    // Will log an MessageImportance.High message to MSBuild. OPTIONAL
    public Action<string> LogInfo { get; set; }

    // Will log a message to MSBuild. OPTIONAL

    // Will log an warning message to MSBuild. OPTIONAL
    public Action<string> LogWarning { get; set; }

    // Will log an warning message to MSBuild at a specific point in the code. OPTIONAL
    public Action<string, SequencePoint> LogWarningPoint { get; set; }

    // Will log an error message to MSBuild. OPTIONAL
    public Action<string> LogError { get; set; }

    // Will log an error message to MSBuild at a specific point in the code. OPTIONAL
    public Action<string, SequencePoint> LogErrorPoint { get; set; }

    // An instance of Mono.Cecil.IAssemblyResolver for resolving assembly references. OPTIONAL
    public IAssemblyResolver AssemblyResolver { get; set; }

    // An instance of Mono.Cecil.ModuleDefinition for processing. REQUIRED
    public ModuleDefinition ModuleDefinition { get; set; }

    // Will contain the full path of the target assembly. OPTIONAL
    public string AssemblyFilePath { get; set; }

    // Will contain the full directory path of the target project. 
    // A copy of $(ProjectDir). OPTIONAL
    public string ProjectDirectoryPath { get; set; }

    // Will contain the full directory path of the current weaver. OPTIONAL
    public string AddinDirectoryPath { get; set; }

    // Will contain the full directory path of the current solution.
    // A copy of `$(SolutionDir)` or, if it does not exist, a copy of `$(MSBuildProjectDirectory)..\..\..\`. OPTIONAL
    public string SolutionDirectoryPath { get; set; }

    // Will contain a semicomma delimetered string that contains 
    // all the references for the target project. 
    // A copy of the contents of the @(ReferencePath). OPTIONAL
    public string References { get; set; }

    // Will a list of all the references marked as copy-local. 
    // A copy of the contents of the @(ReferenceCopyLocalPaths). OPTIONAL
    public List<string> ReferenceCopyLocalPaths { get; set; }

    // Will a list of all the msbuild constants. 
    // A copy of the contents of the $(DefineConstants). OPTIONAL
    public List<string> DefineConstants { get; set; }

    private static readonly MethodInfo _stringJoinMethod;
    private static readonly MethodInfo _stringFormatMethod;
    private static readonly MethodInfo _debugWriteLineMethod;
    private static readonly MethodInfo _getCurrentMethod;
    static ModuleWeaver()
    {
        //Find string.Join(string, object[]) method
        _stringJoinMethod = typeof(string)
            .GetMethods()
            .Where(x => x.Name == nameof(string.Join))
            .Single(x =>
            {
                var parameters = x.GetParameters();
                return parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(string) &&
                    parameters[1].ParameterType == typeof(object[]);
            });

        //Find string.Format(string, object) method
        _stringFormatMethod = typeof(string)
            .GetMethods()
            .Where(x => x.Name == nameof(string.Format))
            .Single(x =>
            {
                var parameters = x.GetParameters();
                return parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(string) &&
                    parameters[1].ParameterType == typeof(object);
            });

        //Find Debug.WriteLine(string) method
        _debugWriteLineMethod = typeof(System.Console)
            .GetMethods()
            .Where(x => x.Name == nameof(Console.WriteLine))
            .Single(x =>
            {
                var parameters = x.GetParameters();
                return parameters.Length == 1 &&
                    parameters[0].ParameterType == typeof(string);
            });
        _getCurrentMethod = typeof(MethodBase).GetMethod("GetCurrentMethod");

    }

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogDebug = m => { };
        LogInfo = m => { };
        LogWarning = m => { };
        LogWarningPoint = (m, p) => { };
        LogError = m => { };
        LogErrorPoint = (m, p) => { };
       

    }

    public void Execute()
    {
        MethodProcessor decorator = new MethodProcessor(ModuleDefinition);
        _referenceFinder = new ReferenceFinder(ModuleDefinition);
        foreach (TypeDefinition type in ModuleDefinition.Types)
        {
            foreach (MethodDefinition method in type.Methods)
            {

                decorator.ProcessMethod(method);
            }
        }
    }

    private void ProcessMethod(MethodDefinition method)
    {
        ILProcessor processor = method.Body.GetILProcessor();
        Instruction current = method.Body.Instructions.First();

        //Create Nop instruction to use as a starting point 
        //for the rest of our instructions
        Instruction first = Instruction.Create(OpCodes.Nop);
        processor.InsertBefore(current, first);
        current = first;

        //Insert all instructions for debug output after Nop
        foreach (Instruction instruction in GetInstructions(method))
        {
            processor.InsertAfter(current, instruction);
            current = instruction;
        }
       // var methodBaseTypeRef = this._referenceFinder.GetTypeReference(typeof(MethodBase));
       // var methodVariableDefinition = AddVariableDefinition(method, "__fody$method", methodBaseTypeRef);
       //var tmp = GetAttributeInstanceInstructions(processor, method, methodVariableDefinition);
       // foreach (var item in tmp)
       // {

       //     processor.InsertAfter(current, item);
       // }
    }

    private IEnumerable<Instruction> GetInstructions(MethodDefinition method)
    {
        #region Get Current Method

        #endregion
        yield return Instruction.Create(OpCodes.Ldstr, $"DEBUG: {method.Name}({{0}})");
        yield return Instruction.Create(OpCodes.Ldstr, ",");

        yield return Instruction.Create(OpCodes.Ldc_I4, method.Parameters.Count);
        yield return Instruction.Create(OpCodes.Newarr, ModuleDefinition.ImportReference(typeof(object)));

        for (int i = 0; i < method.Parameters.Count; i++)
        {
            yield return Instruction.Create(OpCodes.Dup);
            yield return Instruction.Create(OpCodes.Ldc_I4, i);
            yield return Instruction.Create(OpCodes.Ldarg, method.Parameters[i]);
            if (method.Parameters[i].ParameterType.IsValueType)
                yield return Instruction.Create(OpCodes.Box, method.Parameters[i].ParameterType);
            yield return Instruction.Create(OpCodes.Stelem_Ref);
        }

        yield return Instruction.Create(OpCodes.Call, ModuleDefinition.ImportReference(_stringJoinMethod));
        yield return Instruction.Create(OpCodes.Call, ModuleDefinition.ImportReference(_stringFormatMethod));
        yield return Instruction.Create(OpCodes.Call, ModuleDefinition.ImportReference(_debugWriteLineMethod));
    }

    // Will be called when a request to cancel the build occurs. OPTIONAL
    public void Cancel()
    {
    }

    // Will be called after all weaving has occurred and the module has been saved. OPTIONAL
    public void AfterWeaving()
    {
    }

    private static VariableDefinition AddVariableDefinition(MethodDefinition method, string variableName, TypeReference variableType)
    {
        var variableDefinition = new VariableDefinition(variableType);
        method.Body.Variables.Add(variableDefinition);
        return variableDefinition;
    }
    private IEnumerable<Instruction> GetAttributeInstanceInstructions(
           ILProcessor processor,
           MethodDefinition method,
           VariableDefinition methodVariableDefinition)
    {

        var getMethodFromHandleRef = this._referenceFinder.GetMethodReference(typeof(MethodBase), md => md.Name == "GetMethodFromHandle" &&
                                                                                                        md.Parameters.Count == 2);

        var getTypeof = this._referenceFinder.GetMethodReference(typeof(Type), md => md.Name == "GetTypeFromHandle");
        var ctor = this._referenceFinder.GetMethodReference(typeof(Activator), md => md.Name == "CreateInstance" &&
                                                                                        md.Parameters.Count == 1);

        var getCustomAttrs = this._referenceFinder.GetMethodReference(typeof(Attribute),
            md => md.Name == "GetCustomAttributes" &&
            md.Parameters.Count == 2 &&
            md.Parameters[0].ParameterType.FullName == typeof(MemberInfo).FullName &&
            md.Parameters[1].ParameterType.FullName == typeof(Type).FullName);

        /* 
                // Code size       23 (0x17)
                  .maxstack  1
                  .locals init ([0] class SimpleTest.IntersectMethodsMarkedByAttribute i)
                  IL_0000:  nop
                  IL_0001:  ldtoken    SimpleTest.IntersectMethodsMarkedByAttribute
                  IL_0006:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
                  IL_000b:  call       object [mscorlib]System.Activator::CreateInstance(class [mscorlib]System.Type)
                  IL_0010:  castclass  SimpleTest.IntersectMethodsMarkedByAttribute
                  IL_0015:  stloc.0
                  IL_0016:  ret
        */

        var oInstructions = new List<Instruction>
                {
                    processor.Create(OpCodes.Nop),

                    processor.Create(OpCodes.Ldtoken, method),
                    processor.Create(OpCodes.Ldtoken, method.DeclaringType),
                    processor.Create(OpCodes.Call, getMethodFromHandleRef),          // Push method onto the stack, GetMethodFromHandle, result on stack
                    processor.Create(OpCodes.Stloc, methodVariableDefinition),     // Store method in __fody$method

                    processor.Create(OpCodes.Nop),
                };

        

        return oInstructions;
        /*

         * 
        processor.Create(OpCodes.Ldloc_S, methodVariableDefinition),
        processor.Create(OpCodes.Ldtoken, attribute.AttributeType),
        processor.Create(OpCodes.Call, getTypeFromHandleRef),            // Push method + attribute onto the stack, GetTypeFromHandle, result on stack
        processor.Create(OpCodes.Ldc_I4_0),
        processor.Create(OpCodes.Callvirt, getCustomAttributesRef),      // Push false onto the stack (result still on stack), GetCustomAttributes
        processor.Create(OpCodes.Ldc_I4_0),
        processor.Create(OpCodes.Ldelem_Ref),                            // Get 0th index from result
        processor.Create(OpCodes.Castclass, attribute.AttributeType),
        processor.Create(OpCodes.Stloc_S, attributeVariableDefinition)   // Cast to attribute stor in __fody$attribute
        */
    }
}