﻿// <copyright file="StateSetTransformer.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Research.Zen.ModelChecking
{
    using System;
    using System.Collections.Generic;
    using DecisionDiagrams;

    /// <summary>
    /// An input-output set transformer.
    /// </summary>
    public class StateSetTransformer<T1, T2>
    {
        private SolverDD<BDDNode> solver;

        private DD setTransformer;

        private Zen<T1> zenInput;

        private Zen<T2> zenOutput;

        private VariableSet<BDDNode> inputVariables;

        private VariableSet<BDDNode> outputVariables;

        private Dictionary<Type, (object, VariableSet<BDDNode>)> canonicalValues;

        private Dictionary<object, Variable<BDDNode>> arbitraryMapping;

        internal StateSetTransformer(
            SolverDD<BDDNode> solver,
            DD setTransformer,
            (Zen<T1>, VariableSet<BDDNode>) inputAndVariables,
            (Zen<T2>, VariableSet<BDDNode>) outputAndVariables,
            Dictionary<object, Variable<BDDNode>> arbitraryMapping,
            Dictionary<Type, (object, VariableSet<BDDNode>)> canonicalValues)
        {
            this.solver = solver;
            this.setTransformer = setTransformer;
            this.zenInput = inputAndVariables.Item1;
            this.zenOutput = outputAndVariables.Item1;
            this.inputVariables = inputAndVariables.Item2;
            this.outputVariables = outputAndVariables.Item2;
            this.canonicalValues = canonicalValues;
            this.arbitraryMapping = arbitraryMapping;
        }

        /// <summary>
        /// Compute the input set for the transformer.
        /// </summary>
        public StateSet<T1> InputSet(Func<Zen<T1>, Zen<T2>, Zen<bool>> invariant = null)
        {
            DD set = setTransformer;

            if (invariant != null)
            {
                var expr = invariant(this.zenInput, this.zenOutput);

                var symbolicEvaluator = new SymbolicEvaluationVisitor<Assignment<BDDNode>, Variable<BDDNode>, DD, BitVector<BDDNode>>(this.solver);
                var env = new SymbolicEvaluationEnvironment<Assignment<BDDNode>, Variable<BDDNode>, DD, BitVector<BDDNode>>();
                var symbolicResult =
                    (SymbolicBool<Assignment<BDDNode>, Variable<BDDNode>, DD, BitVector<BDDNode>>)expr.Accept(symbolicEvaluator, env);
                var ddOutput = symbolicResult.Value;
                set = this.solver.And(set, ddOutput);
                // Console.WriteLine($"setTransformer: {this.solver.Manager.Display(setTransformer)}");
                // Console.WriteLine($"ddoutput: {this.solver.Manager.Display(ddOutput)}");
                // Console.WriteLine($"set: {this.solver.Manager.Display(set)}");
            }

            var dd = solver.Manager.Exists(set, this.outputVariables);
            var result = new StateSet<T1>(this.solver, dd, this.arbitraryMapping, this.zenInput, this.inputVariables);
            // Console.WriteLine($"result: {this.solver.Manager.Display(result.Set)}");
            return ConvertTo(result, this.canonicalValues[typeof(T1)]);
            // Console.WriteLine($"zen: {this.canonicalValues[typeof(T1)].Item1}");
            // Console.WriteLine($"var: {this.canonicalValues[typeof(T1)].Item2.GetHashCode()}");
            // Console.WriteLine($"curr: {this.inputVariables.GetHashCode()}");
            // Console.WriteLine($"res: {this.solver.Manager.Display(res.Set)}");
        }

        private StateSet<T> ConvertTo<T>(StateSet<T> sourceStateSet, (object, VariableSet<BDDNode>) conversionData)
        {
            return sourceStateSet.ConvertSetVariables(conversionData.Item2, (Zen<T>)conversionData.Item1);
        }

        /// <summary>
        /// Compute the input set for the transformer.
        /// </summary>
        public StateSet<T2> OutputSet(Func<Zen<T1>, Zen<T2>, Zen<bool>> invariant = null)
        {
            DD set = setTransformer;
            if (invariant != null)
            {
                var expr = invariant(this.zenInput, this.zenOutput);

                var symbolicEvaluator = new SymbolicEvaluationVisitor<Assignment<BDDNode>, Variable<BDDNode>, DD, BitVector<BDDNode>>(this.solver);
                var env = new SymbolicEvaluationEnvironment<Assignment<BDDNode>, Variable<BDDNode>, DD, BitVector<BDDNode>>();
                var symbolicResult =
                    (SymbolicBool<Assignment<BDDNode>, Variable<BDDNode>, DD, BitVector<BDDNode>>)expr.Accept(symbolicEvaluator, env);
                var ddInput = symbolicResult.Value;
                set = this.solver.And(set, ddInput);
            }

            var dd = solver.Manager.Exists(set, inputVariables);
            var result = new StateSet<T2>(this.solver, dd, this.arbitraryMapping, this.zenOutput, this.outputVariables);
            return ConvertTo(result, this.canonicalValues[typeof(T2)]);
        }

        /// <summary>
        /// Compute the input set for the transformer.
        /// </summary>
        public StateSet<T2> TransformForward(StateSet<T1> input)
        {
            Console.WriteLine($"incoming: {this.solver.Manager.Display(input.Set)}");
            input = ConvertTo(input, (this.zenInput, this.inputVariables));
            Console.WriteLine($"convertIn: {this.solver.Manager.Display(input.Set)}");
            DD set = input.Set;
            DD dd = this.solver.Manager.And(set, this.setTransformer);
            dd = this.solver.Manager.Exists(dd, this.inputVariables);
            var result = new StateSet<T2>(this.solver, dd, this.arbitraryMapping, this.zenOutput, this.outputVariables);
            Console.WriteLine($"result: {this.solver.Manager.Display(result.Set)}");
            var res = ConvertTo(result, this.canonicalValues[typeof(T2)]);
            Console.WriteLine($"convertOut: {this.solver.Manager.Display(res.Set)}");
            return res;
        }

        /// <summary>
        /// Compute the input set for the transformer.
        /// </summary>
        public StateSet<T1> TransformBackwards(StateSet<T2> output)
        {
            output = ConvertTo(output, (this.zenOutput, this.outputVariables));
            DD set = output.Set;
            DD dd = this.solver.Manager.And(set, this.setTransformer);
            dd = this.solver.Manager.Exists(dd, this.outputVariables);
            var result = new StateSet<T1>(this.solver, dd, this.arbitraryMapping, this.zenInput, this.inputVariables);
            return ConvertTo(result, this.canonicalValues[typeof(T1)]);
        }
    }
}