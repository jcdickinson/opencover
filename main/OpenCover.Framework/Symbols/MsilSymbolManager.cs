using log4net;
using Mono.Cecil;
using OpenCover.Framework.Model;
using OpenCover.Framework.Strategy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCover.Framework.Symbols
{
    internal class MsilSymbolManager : ISymbolManager
    {
        private ICommandLine _commandLine;
        private IFilter _filter;
        private ILog _logger;
        private ITrackedMethodStrategyManager _trackedMethodStrategyManager;
        private AssemblyDefinition _sourceAssembly;
        private Dictionary<int, MethodDefinition> _methods;

        public string ModulePath
        {
            get;
            set;
        }

        public string ModuleName
        {
            get;
            set;
        }

        public MsilSymbolManager(ICommandLine commandLine, IFilter filter, log4net.ILog logger, Strategy.ITrackedMethodStrategyManager trackedMethodStrategyManager)
        {
            _commandLine = commandLine;
            _filter = filter;
            _logger = logger;
            _trackedMethodStrategyManager = trackedMethodStrategyManager;
            _methods = new Dictionary<int, MethodDefinition>();
        }

        internal void Initialise(string modulePath, string moduleName)
        {
            ModulePath = modulePath;
            ModuleName = moduleName;
        }

        public AssemblyDefinition SourceAssembly
        {
            get
            {
                if (_sourceAssembly == null)
                {
                    var currentPath = Environment.CurrentDirectory;
                    try
                    {
                        _sourceAssembly = AssemblyDefinition.ReadAssembly(ModulePath);
                    }
                    catch (Exception)
                    {
                        // failure to here is quite normal for DLL's with no PDBs => no instrumentation
                        _sourceAssembly = null;
                    }

                    if (_sourceAssembly == null)
                    {
                        if (_logger.IsDebugEnabled)
                        {
                            _logger.DebugFormat("Cannot instrument {0} as no assembly could be loaded", ModulePath);
                        }
                    }
                }
                return _sourceAssembly;
            }
        }

        public Model.File[] GetFiles()
        {
            return _sourceAssembly.Modules.Select(x => new Model.File()
            {
                FullPath = ModulePath + "::" + x.Name
            }).ToArray();
        }

        public Model.Class[] GetInstrumentableTypes()
        {
            return _sourceAssembly.Modules
                .SelectMany(x => x.Types)
                .SelectMany(RecurseTypes)
                .Where(x => !x.IsEnum && !x.IsInterface)
                .Select(x => Filter(x, new Model.Class()
                {
                    Files = new Model.File[] 
                    { 
                        new Model.File()
                        {
                            FullPath = ModulePath + "::" + x.Module.Name
                        }
                    },
                    FullName = x.FullName
                })).ToArray();
        }

        private IEnumerable<TypeDefinition> RecurseTypes(TypeDefinition def)
        {
            yield return def;
            foreach (var item in def.NestedTypes)
            {
                foreach (var r in RecurseTypes(item))
                {
                    yield return r;
                }
            }
        }

        public Model.Method[] GetMethodsForType(Model.Class type, Model.File[] files)
        {
            return _sourceAssembly.Modules
                .SelectMany(x => x.Types)
                .SelectMany(RecurseTypes)
                .Where(x => x.FullName == type.FullName)
                .Take(1)
                .SelectMany(x => x.Methods)
                .Where(x => x.HasBody && !x.IsGetter && !x.IsSetter && !x.IsAbstract)
                .Select(x => _methods[x.MetadataToken.ToInt32()] = x)
                .Select(x => Filter(x, BuildMethod(files, x))).ToArray();
        }

        private Method BuildMethod(IEnumerable<Model.File> files, MethodDefinition methodDefinition)
        {
            var method = new Method
            {
                Name = methodDefinition.FullName,
                IsConstructor = methodDefinition.IsConstructor,
                IsStatic = methodDefinition.IsStatic,
                IsGetter = methodDefinition.IsGetter,
                IsSetter = methodDefinition.IsSetter,
                MetadataToken = methodDefinition.MetadataToken.ToInt32()
            };

            if (methodDefinition.SafeGetMethodBody() == null)
            {
                if (methodDefinition.IsNative)
                    method.MarkAsSkipped(SkippedMethod.NativeCode);
                else
                    method.MarkAsSkipped(SkippedMethod.Unknown);
                return method;
            }

            if (_filter.ExcludeByAttribute(methodDefinition))
                method.MarkAsSkipped(SkippedMethod.Attribute);

            var definition = methodDefinition;
            method.FileRef = files.Where(x => x.FullPath == ModulePath + "::" + methodDefinition.Module.Name)
                .Select(x => new FileRef { UniqueId = x.UniqueId }).FirstOrDefault();
            return method;
        }

        public Model.SequencePoint[] GetSequencePointsForToken(int token)
        {
            var list = new List<Model.SequencePoint>(1);
            MethodDefinition def;
            if (_methods.TryGetValue(token, out def) & def.HasBody)
            {
                foreach (var instruction in def.Body.Instructions.Take(1))
                {
                    list.Add(new SequencePoint
                    {
                        EndColumn = instruction.Offset,
                        EndLine = instruction.Offset,
                        Offset = instruction.Offset,
                        Ordinal = (uint)list.Count,
                        StartColumn = instruction.Offset,
                        StartLine = instruction.Offset,
                        Document = ModulePath + "::" + def.Module.Name
                    });
                }
            }
            return list.ToArray();
        }

        public Model.BranchPoint[] GetBranchPointsForToken(int token)
        {
            return null;
        }

        public int GetCyclomaticComplexityForToken(int token)
        {
            return 0;
        }

        public Model.TrackedMethod[] GetTrackedMethods()
        {
            if (SourceAssembly == null) return null;
            return _trackedMethodStrategyManager.GetTrackedMethods(ModulePath);
        }

        private Model.Class Filter(TypeDefinition def, Model.Class cls)
        {
            if (!_filter.InstrumentClass(def.Module.Name, def.FullName))
            {
                cls.MarkAsSkipped(SkippedMethod.Filter);
            }
            else if (_filter.ExcludeByAttribute(def))
            {
                cls.MarkAsSkipped(SkippedMethod.Attribute);
            }
            return cls;
        }

        private Model.Method Filter(MethodDefinition def, Model.Method mth)
        {
            if (!_filter.InstrumentClass(def.Module.Name, def.FullName))
            {
                mth.MarkAsSkipped(SkippedMethod.Filter);
            }
            else if (_filter.ExcludeByAttribute(def))
            {
                mth.MarkAsSkipped(SkippedMethod.Attribute);
            }
            return mth;
        }
    }
}
