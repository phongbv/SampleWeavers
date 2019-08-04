using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Example.Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;


public class MethodProcessor
{
    private readonly ReferenceFinder _referenceFinder;
    public MethodProcessor(ModuleDefinition moduleDefinition)
    {
        _referenceFinder = new ReferenceFinder(moduleDefinition);
    }
    protected internal void ProcessMethod(MethodDefinition method)
    {
        ILProcessor processor = method.Body.GetILProcessor();
        Instruction current = method.Body.Instructions.First();
        //Create Nop instruction to use as a starting point 
        //for the rest of our instructions
        Instruction first = Instruction.Create(OpCodes.Nop);
        processor.InsertBefore(current, first);
        current = first;

        var methodBaseTypeRef = this._referenceFinder.GetTypeReference(typeof(MethodBase));


        var parameterTypeRef = this._referenceFinder.GetTypeReference(typeof(object));

        var methodInitTypeRef = this._referenceFinder.GetTypeReference(typeof(MethodDefinition));
        VariableDefinition parametersVariableDefinition = null;
        var parametersArrayTypeRef = new ArrayType(parameterTypeRef);
        parametersVariableDefinition = AddVariableDefinition(method, "__fody$parameters", parametersArrayTypeRef);
        if (parametersVariableDefinition != null)
        {
            var printParams = CreateParametersArrayInstructions(processor, method, parameterTypeRef, parametersVariableDefinition);
            foreach (var instruction in printParams)
            {

                processor.InsertAfter(current, instruction);
                current = instruction;
            }
        }
        var getInstanceMethod = this._referenceFinder.GetOptionalMethodReference(this._referenceFinder.GetTypeReference(typeof(MethodDecorator)),
               md => md.Name == "get_Instance");
        #region Gọi Init
        var initMethodRef0 = this._referenceFinder.GetOptionalMethodReference(this._referenceFinder.GetTypeReference(typeof(MethodDecorator)),
               md => md.Name == "Init" && md.Parameters.Count == 3);
        if (initMethodRef0 != null)
        {
            var getCurrentMethodBase = this._referenceFinder.GetOptionalMethodReference(this._referenceFinder.GetTypeReference(typeof(MethodBase)),
              md => md.Name == nameof(MethodBase.GetCurrentMethod));
            var callInitInstructions = GetInitInstructions(processor, getInstanceMethod, getCurrentMethodBase, parametersVariableDefinition, initMethodRef0);
            foreach (var instruction in callInitInstructions)
            {

                processor.InsertAfter(current, instruction);
                current = instruction;
            }
        }


        #endregion
        #region Gọi OnEntry
        var onEntryMethodRef0 = this._referenceFinder.GetOptionalMethodReference(this._referenceFinder.GetTypeReference(typeof(MethodDecorator)),
                md => md.Name == "OnEntry" && md.Parameters.Count == 0);
        if (onEntryMethodRef0 != null)
        {

            var attributeVariableDefinition = AddVariableDefinition(method, "decorator", this._referenceFinder.GetTypeReference(typeof(MethodDecorator)));
            var callOnEntryInstructions = GetCallOnEntryInstructions(processor, getInstanceMethod, onEntryMethodRef0);
            foreach (var instruction in callOnEntryInstructions)
            {

                processor.InsertAfter(current, instruction);
                current = instruction;
            }
        }
        #endregion
        var onExitMethodRef = this._referenceFinder.GetOptionalMethodReference(this._referenceFinder.GetTypeReference(typeof(MethodDecorator)),
               md => md.Name == "OnExit");
        // Chua chay duoc
        var lastIntruction = method.Body.Instructions.Last();
        var tmp = method.Body.Instructions.Last();
        foreach (var instruction in CreateOnExitInstructions(processor, getInstanceMethod, onExitMethodRef))
        {
            processor.InsertAfter(lastIntruction, instruction);
            lastIntruction = instruction;
        }
        processor.InsertAfter(tmp, Instruction.Create(OpCodes.Leave_S, lastIntruction));
        
    }
    private static IEnumerable<Instruction> GetInitInstructions(ILProcessor processor,
           MethodReference getInstanceMethodRef,
           MethodReference getCurrentMethodBase,
           VariableDefinition methodBase,
           MethodReference onInitMethodRef)
    {
        return new List<Instruction>
                   {
                       processor.Create(OpCodes.Call, getInstanceMethodRef),
                       processor.Create(OpCodes.Ldnull),
                       processor.Create(OpCodes.Call, getCurrentMethodBase),
                       processor.Create(OpCodes.Ldloc, methodBase),
                        processor.Create(OpCodes.Callvirt, onInitMethodRef)
                   };
    }
    private static IEnumerable<Instruction> GetCallOnEntryInstructions(
           ILProcessor processor,
           MethodReference getInstanceMethodRef,
           MethodReference onEntryMethodRef)
    {
        // Call __fody$attribute.OnEntry()
        return new List<Instruction>
                   {
                       processor.Create(OpCodes.Call, getInstanceMethodRef),
                       processor.Create(OpCodes.Callvirt, onEntryMethodRef),
                   };
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

        var getMethodFromHandleRef = this._referenceFinder.GetMethodReference(typeof(MethodBase), md => md.Name == nameof(MethodBase.GetMethodFromHandle) &&
                                                                                                        md.Parameters.Count == 2);

        var getTypeof = this._referenceFinder.GetMethodReference(typeof(Type), md => md.Name == nameof(Type.GetTypeFromHandle));
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

    private static IEnumerable<Instruction> CreateParametersArrayInstructions(ILProcessor processor, MethodDefinition method, TypeReference objectTypeReference /*object*/, VariableDefinition arrayVariable /*parameters*/)
    {
        var createArray = new List<Instruction> {
                processor.Create(OpCodes.Ldc_I4, method.Parameters.Count),  //method.Parameters.Count
                processor.Create(OpCodes.Newarr, objectTypeReference),      // new object[method.Parameters.Count]
                processor.Create(OpCodes.Stloc, arrayVariable)              // var objArray = new object[method.Parameters.Count]
            };

        foreach (var p in method.Parameters)
            createArray.AddRange(IlHelper.ProcessParam(p, arrayVariable));

        return createArray;
    }

    public static IEnumerable<Instruction> CreateOnExitInstructions(ILProcessor processor, MethodReference getInstanceMethodRef, MethodReference onExitMethodRef)
    {
        return new List<Instruction>
                   {
                       processor.Create(OpCodes.Call, getInstanceMethodRef),
                       processor.Create(OpCodes.Callvirt, onExitMethodRef),
                       processor.Create(OpCodes.Rethrow),
                       processor.Create(OpCodes.Ret),
                   };
    }
}

