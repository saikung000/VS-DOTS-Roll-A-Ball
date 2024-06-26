using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityEditor.VisualScripting.Model
{
    [Serializable]
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    // virtual PortModel getters to allow for Moq
    public class BinaryOperatorNodeModel : NodeModel, IOperationValidator, IHasMainOutputPort
    {
        public BinaryOperatorKind kind;
        static Type[] s_SortedNumericTypes =
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(float),
            typeof(long),
            typeof(ulong),
            typeof(double),
            typeof(decimal)
        };

        public override string Title => kind.ToString();

        public enum PortName
        {
            PortA, PortB
        }

        PortModel m_InputAPort;
        PortModel m_InputBPort;
        PortModel m_MainOutputPort;

        // virtual to allow for Moq
        public virtual IPortModel InputPortA => m_InputAPort;
        public virtual IPortModel InputPortB => m_InputBPort;
        public virtual IPortModel OutputPort => m_MainOutputPort;

        public IPortModel GetPort(PortName portName)
        {
            return portName == PortName.PortA ? InputPortA : InputPortB;
        }

        protected override void OnDefineNode()
        {
            m_InputAPort = AddDataInput<Unknown>("A", nameof(PortName.PortA));
            m_InputBPort = AddDataInput<Unknown>("B", nameof(PortName.PortB));
            m_MainOutputPort = AddDataOutputPort<Unknown>("Out");
        }

        static bool IsBooleanOperatorKind(BinaryOperatorKind kind)
        {
            switch (kind)
            {
                case BinaryOperatorKind.Equals:
                case BinaryOperatorKind.NotEqual:
                case BinaryOperatorKind.GreaterThan:
                case BinaryOperatorKind.GreaterThanOrEqual:
                case BinaryOperatorKind.LessThan:
                case BinaryOperatorKind.LessThanOrEqual:
                    return true;
            }
            return false;
        }

        static int GetNumericTypePriority(Type type)
        {
            return Array.IndexOf(s_SortedNumericTypes, type);
        }

        static Type GetBiggestNumericType(Type x, Type y)
        {
            return GetNumericTypePriority(x) > GetNumericTypePriority(y) ? x : y;
        }

        public static Type GetOutputTypeFromInputs(BinaryOperatorKind kind, Type x, Type y)
        {
            List<MethodInfo> operators = TypeSystem.GetBinaryOperators(kind, x, y);
            if (IsBooleanOperatorKind(kind))
                return operators.Any() ? operators[0].ReturnType : typeof(bool);

            // TODO handle multiplying numeric types together: float*float=double? etc.
            // An idea was to use Roslyn to generate a lookup table for arithmetic operations
            if (operators.Count >= 1 && operators.All(o => o.ReturnType == operators[0].ReturnType)) // all operators have the same return type
                return operators[0].ReturnType;
            if (x == null && y == null)                // both null
                return typeof(Unknown);
            if (x == null || y == null)                // one is null
                return x ?? y;
            if (x == y)                                // same type
                return x;
            if (x.IsNumeric() && y.IsNumeric())        // both numeric types
                return GetBiggestNumericType(x, y);

            return typeof(Unknown);
        }

        public override void OnConnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel)
        {
            var x = m_InputAPort.ConnectionPortModels.FirstOrDefault()?.DataType;
            if (x != null)
                m_InputAPort.DataType = x.Value;
            var y = m_InputBPort.ConnectionPortModels.FirstOrDefault()?.DataType;
            if (y != null)
                m_InputBPort.DataType = y.Value;

            //TODO A bit ugly of a hack... evaluate a better approach?
            m_MainOutputPort.DataType = GetOutputTypeFromInputs(kind, x?.Resolve(Stencil), y?.Resolve(Stencil)).GenerateTypeHandle(Stencil);
        }

        public override void OnDisconnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel)
        {
            OnConnection(selfConnectedPortModel, otherConnectedPortModel);
        }

        public bool HasValidOperationForInput(IPortModel port, TypeHandle typeHandle)
        {
            PortName portName = ReferenceEquals(port, InputPortA) ? PortName.PortA : PortName.PortB;
            var otherPort = portName == PortName.PortA ? InputPortB : InputPortA;
            var dataType = typeHandle.Resolve(Stencil);

            if (otherPort.Connected)
            {
                Type otherPortType = otherPort.DataType.Resolve(Stencil);

                return portName == PortName.PortB
                    ? TypeSystem.IsBinaryOperationPossible(otherPortType, dataType, kind)
                    : TypeSystem.IsBinaryOperationPossible(dataType, otherPortType, kind);
            }

            return TypeSystem.GetOverloadedBinaryOperators(dataType).Contains(kind);
        }
    }
}
