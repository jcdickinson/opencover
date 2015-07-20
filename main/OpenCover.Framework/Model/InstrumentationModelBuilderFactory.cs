﻿//
// OpenCover - S Wilde
//
// This source code is released under the MIT License; see the accompanying license file.
//

using System.Collections.Generic;
using System.Linq;
using OpenCover.Framework.Strategy;
using OpenCover.Framework.Symbols;
using log4net;

namespace OpenCover.Framework.Model
{
    public class InstrumentationModelBuilderFactory : IInstrumentationModelBuilderFactory
    {
        private readonly ICommandLine _commandLine;
        private readonly IFilter _filter;
        private readonly ILog _logger;
        private readonly ITrackedMethodStrategyManager _trackedMethodStrategyManager;

        public InstrumentationModelBuilderFactory(ICommandLine commandLine, IFilter filter, ILog logger, ITrackedMethodStrategyManager trackedMethodStrategyManager)
        {
            _commandLine = commandLine;
            _filter = filter;
            _logger = logger;
            _trackedMethodStrategyManager = trackedMethodStrategyManager;
        }

        public IInstrumentationModelBuilder CreateModelBuilder(string modulePath, string moduleName)
        {
            var manager = new MsilSymbolManager(_commandLine, _filter, _logger, _trackedMethodStrategyManager);
            manager.Initialise(modulePath, moduleName);
            return new InstrumentationModelBuilder(manager);
        }

    }
}