﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CodeElements.NetworkCallTransmission.Proxy
{
    internal class EventInterceptorImplementor
    {
        public FieldBuilder InterceptorField { get; private set; }
        public FieldBuilder EventsField { get; private set; }

        public void ImplementProxy(TypeBuilder typeBuilder)
        {
            // Implement the IAsyncInterceptorProxy interface
            typeBuilder.AddInterfaceImplementation(typeof(IEventInterceptorProxy));

            InterceptorField = ImplementorHelper.ImplementProperty(typeBuilder,
                nameof(IEventInterceptorProxy.Interceptor),
                typeof(IEventInterceptor), typeof(IEventInterceptorProxy));

            EventsField = ImplementorHelper.ImplementProperty(typeBuilder, nameof(IEventInterceptorProxy.Events),
                typeof(EventInfo[]), typeof(IEventInterceptorProxy));
        }

        public void ImplementTriggerEvent(TypeBuilder typeBuilder, FieldBuilder[] fieldBuilders, IList<EventInfo> events)
        {
            var methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            var methodBuilder = typeBuilder.DefineMethod(nameof(IEventInterceptorProxy.TriggerEvent), methodAttributes,
                CallingConventions.HasThis, typeof(void), new[] {typeof(int), typeof(object)});

            var il = methodBuilder.GetILGenerator();
            var jumpTable = new Label[fieldBuilders.Length];

            for (int i = 0; i < fieldBuilders.Length; i++)
                jumpTable[i] = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Switch, jumpTable);

            //default case
            il.Emit(OpCodes.Ldstr, "The event id was not found.");

            il.Emit(OpCodes.Newobj,
                typeof(ArgumentException).GetConstructors()
                    .First(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == typeof(string)));
            il.Emit(OpCodes.Throw);

            for (int i = 0; i < fieldBuilders.Length; i++)
            {
                var ifNotNullLabel = il.DefineLabel();

                il.MarkLabel(jumpTable[i]);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, fieldBuilders[i]);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue_S, ifNotNullLabel);

                //if null
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ret);

                //if not null
                il.MarkLabel(ifNotNullLabel);
                il.Emit(OpCodes.Ldarg_0);

                var eventInfo = events[i];
                if (eventInfo.EventHandlerType.IsGenericType)
                {
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Castclass, eventInfo.EventHandlerType.GetGenericArguments()[0]);
                }
                else
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    il.Emit(OpCodes.Ldsfld,
                        typeof(EventArgs).GetField(nameof(EventArgs.Empty), BindingFlags.Static | BindingFlags.Public));
                }

                il.Emit(OpCodes.Callvirt, eventInfo.EventHandlerType.GetMethod(nameof(EventHandler.Invoke)));
                il.Emit(OpCodes.Ret);
            }

            typeBuilder.DefineMethodOverride(methodBuilder,
                typeof(IEventInterceptorProxy).GetMethod(nameof(IEventInterceptorProxy.TriggerEvent)));
        }
    }
}