using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lucaniss.Tools.DynamicMocks.Extensions;


namespace Lucaniss.Tools.DynamicMocks.Implementation
{
    internal static class MSILCodeGenerator
    {
        public static LocalBuilder DeclareLocalVariable(ILGenerator msil, Type type)
        {
            // INFO: Zadeklarowanie zmiennej lokalnej o podanym typie.
            return msil.DeclareLocal(type);
        }

        public static void CreateArrayForArgumentTypes(ILGenerator msil, MethodInfo methodInfo, LocalBuilder arrayVariable)
        {
            // INFO: Stworzenie tablicy przechowuj�cej nazwy typ�w argument�w metody oraz przypisanie jej do zmiennej lokalnej.
            //       1. Od�o�enie na stosie liczby okre�laj�cej rozmiar tablicy.
            //       2. Utworzenie na stosie tablicy o rozmiarze wskazanym przez warto�� na stosie.
            msil.Emit(OpCodes.Ldc_I4, methodInfo.GetParameters().Count());
            msil.Emit(OpCodes.Newarr, typeof (String));

            // INFO: Zdj�cie ze stosu nowo utworzonej tablicy i przypisanie jej do zmiennej lokalnej.
            msil.Emit(OpCodes.Stloc, arrayVariable);
        }

        public static void CraeteArrayForArgumentValues(ILGenerator msil, MethodInfo methodInfo, LocalBuilder arrayVariable)
        {
            // INFO: Stworzenie tablicy przechowuj�cej warto�ci argument�w metody oraz przypisanie jej do zmiennej lokalnej.
            //       1. Od�o�enie na stosie liczby okre�laj�cej rozmiar tablicy.
            //       2. Utworzenie na stosie tablicy o rozmiarze wskazanym przez warto�� na stosie.
            msil.Emit(OpCodes.Ldc_I4, methodInfo.GetParameters().Count());
            msil.Emit(OpCodes.Newarr, typeof (Object));

            // INFO: Zdj�cie ze stosu nowo utworzonej tablicy i przypisanie jej do zmiennej lokalnej.
            msil.Emit(OpCodes.Stloc, arrayVariable);
        }

        public static void StoreArgumentTypeNameInArray(ILGenerator msil, ParameterInfo parameter, Int32 index, LocalBuilder arrayVariable)
        {
            // INFO: Od�o�enie na stosie element�w potrzebnych do skopiowania warto�ci parametru metody do tablicy obiekt�w.
            msil.Emit(OpCodes.Ldloc, arrayVariable); // Od�o�enie na stosie zmiennej lokalnej reprezentuj�cej tablic� obiekt�w.
            msil.Emit(OpCodes.Ldc_I4, index); // Od�o�enie na stosie liczby reprezentuj�cej indeks elementu tablicy do kt�rego ma zosta� skopiowana warto��.
            msil.Emit(OpCodes.Ldstr, parameter.ParameterType.SafeGetTypeName()); // Od�o�enie na stosie nazwy typu argumentu metody.

            // INFO: Skopiowanie nazwy typu argumentu metody do tablicy obiekt�w.
            msil.Emit(OpCodes.Stelem_Ref);
        }

        public static void StoreArgumentValueInArray(ILGenerator msil, ParameterInfo parameter, Int32 index, LocalBuilder arrayVariable)
        {
            // INFO: Od�o�enie na stosie element�w potrzebnych do skopiowania warto�ci parametru metody do tablicy obiekt�w.
            msil.Emit(OpCodes.Ldloc, arrayVariable); // Od�o�enie na stosie zmiennej lokalnej reprezentuj�cej tablic� obiekt�w.
            msil.Emit(OpCodes.Ldc_I4, index); // Od�o�enie na stosie liczby reprezentuj�cej indeks elementu tablicy do kt�rego ma zosta� skopiowana warto��.          

            // INFO: Od�o�enie na stosie warto�ci argumentu metody o podanym indeksie.
            //       W przypadku parametr�w referencyjnych (ref, out) odk�adany jest adres.
            msil.Emit(OpCodes.Ldarg, index + 1);

            // INFO: Je�li parametr jest przekazywany przez referencj� (ref, out).
            if (parameter.ParameterType.IsByRef)
            {
                if (parameter.IsValueOrPrimitiveType())
                {
                    // INFO: Od�o�enie na stosie obiektu wskazywanego przez adres argumentu metody o podanym indeksie (dla typ�w warto�ciowych).
                    msil.Emit(OpCodes.Ldobj, parameter.SafeGetType());
                }
                else
                {
                    // INFO: Od�o�enie na stosie referencji argumentu metody o podanym indeksie (dla typ�w referencyjnych).
                    msil.Emit(OpCodes.Ldind_Ref);
                }
            }

            // INFO: Je�li parametr jest typem prostym wtedy wymagana jest operacja 'Box'.
            if (parameter.IsValueOrPrimitiveType())
            {
                // INFO: Wykonanie operacji 'Box' na aktualnym elemencie stosu (warto��/referencja argumentu metody).
                msil.Emit(OpCodes.Box, parameter.SafeGetType());
            }

            // INFO: Skopiowanie warto�ci argumentu metody do tablicy obiekt�w.
            msil.Emit(OpCodes.Stelem_Ref);
        }

        public static void InvokeMockInterceptorMethod(ILGenerator msil, MethodInfo methodInfo, LocalBuilder arrayForArgumentTypesVariable, LocalBuilder arrayForArgumentValuesVariable)
        {
            // INFO: Od�o�enie na stosie wska�nika 'this'.
            msil.Emit(OpCodes.Ldarg_0);

            // INFO: Od�o�enie na stosie nazwy metody.
            msil.Emit(OpCodes.Ldstr, methodInfo.Name);

            // INFO: Od�o�enie na stosie tablicy obiekt�w (Nazwy typ�w argument�w metody).
            msil.Emit(OpCodes.Ldloc, arrayForArgumentTypesVariable);

            // INFO: Od�o�enie na stosie tablicy obiekt�w (Warto�ci argument�w metody).
            msil.Emit(OpCodes.Ldloc, arrayForArgumentValuesVariable);

            // INFO: Wywo�anie metody interceptora. Je�li metoda zwraca warto�� to ta warto�� jest odk�adana na stos.
            msil.Emit(OpCodes.Call, MockObject.GetInterceptorMethodInfo());
        }

        public static void HandleMockInterceptorMethodReturnValue(ILGenerator msil, MethodInfo methodInfo)
        {
            // INFO: Je�li zwracana warto�� jest typu warto�ciowego wtedy wymagana jest operacja 'Unbox'.
            if (methodInfo.ReturnType != typeof (void) && (methodInfo.ReturnType.IsValueOrPrimitiveType()))
            {
                // INFO: Wykonanie operacji 'Unbox' (na wskazany typ) na aktualnym elemencie stosu.
                msil.Emit(OpCodes.Unbox_Any, methodInfo.ReturnType);
            }
        }

        public static void AssignReferenceArgumentValues(ILGenerator msil, ParameterInfo[] parameters, LocalBuilder arrayForParameterValues)
        {
            for (var index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].ParameterType.IsByRef)
                {
                    // INFO: Od�o�enie na stosie warto�ci argumentu metody (w tym wypadku adres).
                    msil.Emit(OpCodes.Ldarg, index + 1);

                    // INFO: Od�o�enie na stosie referencji ze zmiennej lokalnej tablicowej o podanym indeksie.
                    msil.Emit(OpCodes.Ldloc, arrayForParameterValues);
                    msil.Emit(OpCodes.Ldc_I4, index);
                    msil.Emit(OpCodes.Ldelem_Ref);

                    if (parameters[index].IsValueOrPrimitiveType())
                    {
                        // INFO: Skopiowanie obiektu o typie warto�ciowym pod wskazany adres argumentu o podanym indeksie (wymaga operacji 'Unbox').
                        msil.Emit(OpCodes.Unbox_Any, parameters[index].ParameterType.GetElementType());
                        msil.Emit(OpCodes.Stobj, parameters[index].ParameterType.GetElementType());
                    }
                    else
                    {
                        // INFO: Skopiowanie obiektu o typie referencyjnym pod wskazany adres argumentu o podanym indeksie.
                        msil.Emit(OpCodes.Stind_Ref);
                    }
                }
            }
        }

        public static void ReturnFromMethod(ILGenerator msil, MethodInfo methodInfo)
        {
            // INFO: Poniewa� metoda interceptora zwraca zawsze warto�� (obiekt o typie referencyjnyn) 
            //       to dla metod kt�re nie zwracaj� warto�ci (void) nale�y zdj�� warto�� ze stosu.
            if (methodInfo.ReturnType == typeof (void))
            {
                msil.Emit(OpCodes.Pop);
            }

            // INFO: Powr�t z metody.
            msil.Emit(OpCodes.Ret);
        }
    }
}