﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using WebAssembly.Instructions;

namespace WebAssembly.Runtime
{
    /// <summary>
    /// Tests the <see cref="FunctionTable"/> class.
    /// </summary>
    [TestClass]
    public class TableImportTests
    {
        /// <summary>
        /// Tests adding a function delegate to an imported table.
        /// </summary>
        [TestMethod]
        public void Compile_TableImport_AddFunction()
        {
            var module = new Module();
            module.Types.Add(new Type
            {
                Returns = new[] { ValueType.Int32 },
                Parameters = new[] { ValueType.Int32 }
            });
            module.Imports.Add(new Import.Table
            {
                Module = "Test",
                Field = "Test",
                Definition = new Table
                {
                    ElementType = ElementType.AnyFunction,
                    ResizableLimits = new ResizableLimits(1)
                }
            });
            module.Functions.Add(new Function
            {
            });
            module.Exports.Add(new Export
            {
                Name = "Test",
            });
            module.Elements.Add(new Element
            {
                Elements = new uint[] { 0 },
                InitializerExpression = new Instruction[]
                {
                    new Int32Constant(0),
                    new End(),
                },
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetLocal(0),
                    new End()
                },
            });

            var table = new FunctionTable(1);
            Assert.IsNull(table[0]);

            var compiled = module.ToInstance<CompilerTestBase<int>>(
                new ImportDictionary {
                    { "Test", "Test", table },
                });

            var rawDelegate = table[0];
            Assert.IsNotNull(rawDelegate);
            Assert.IsInstanceOfType(rawDelegate, typeof(Func<int, int>));
            var nativeDelegate = (Func<int, int>)rawDelegate;
            Assert.AreEqual(0, nativeDelegate(0));
            Assert.AreEqual(5, nativeDelegate(5));
        }

        /// <summary>
        /// Tests a function delegate already present on an imported table.
        /// </summary>
        [TestMethod]
        public void Compile_TableImport_ExistingFunction()
        {
            var module = new Module();
            module.Types.Add(new Type
            {
                Returns = new[] { ValueType.Int32 },
                Parameters = new[] { ValueType.Int32 }
            });
            module.Imports.Add(new Import.Table
            {
                Module = "Test",
                Field = "Test",
                Definition = new Table
                {
                    ElementType = ElementType.AnyFunction,
                    ResizableLimits = new ResizableLimits(1)
                }
            });
            module.Functions.Add(new Function
            {
            });
            module.Exports.Add(new Export
            {
                Name = "Test",
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetLocal(0),
                    new Int32Constant(0),
                    new CallIndirect(0),
                    new End()
                },
            });

            var table = new FunctionTable(1);
            var calls = 0;
            table[0] = new Func<int, int>(value => { calls++; return value + 1; });

            var compiled = module.ToInstance<CompilerTestBase<int>>(
                new ImportDictionary {
                    { "Test", "Test", table },
                });

            Assert.AreEqual(0, calls);
            Assert.AreEqual(3, compiled.Exports.Test(2));
            Assert.AreEqual(1, calls);
        }

        /// <summary>
        /// Runs the sample WASM from https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/WebAssembly/Table .
        /// </summary>
        [TestMethod]
        public void Execute_Sample_MDN_Table2()
        {
            var tbl = new FunctionTable(2);
            Assert.AreEqual(2u, tbl.Length);
            Assert.IsNull(tbl[0]);
            Assert.IsNull(tbl[1]);

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WebAssembly.Samples.table2.wasm"))
            {
                var imports = new ImportDictionary
                {
                    { "js", "tbl", tbl },
                };
                Compile.FromBinary<dynamic>(stream)(imports);
            }

            Assert.AreEqual(2u, tbl.Length);

            var f1 = tbl[0];
            Assert.IsNotNull(f1);
            Assert.IsInstanceOfType(f1, typeof(Func<int>));
            Assert.AreEqual(42, ((Func<int>)f1).Invoke());

            var f2 = tbl[1];
            Assert.IsNotNull(f2);
            Assert.IsInstanceOfType(f1, typeof(Func<int>));
            Assert.AreEqual(83, ((Func<int>)f2).Invoke());
        }

        /// <summary>
        /// Used to test table export functionality via tests like <see cref="Compile_TableImport_ExportedButNotUsedInternally"/>.
        /// </summary>
        public abstract class ExportedTable
        {
            /// <summary>
            /// An exported table.
            /// </summary>
            public abstract FunctionTable Table { get; }
        }

        /// <summary>
        /// Tests exporting a function table that wasn't imported or defined.
        /// </summary>
        [TestMethod]
        public void Compile_TableImport_ExportedButNotUsedInternally()
        {
            var module = new Module();
            module.Exports.Add(new Export
            {
                Name = nameof(ExportedTable.Table),
                Kind = ExternalKind.Table,
            });

            Assert.ThrowsException<ModuleLoadException>(() => module.ToInstance<ExportedTable>(new ImportDictionary()));
        }

        /// <summary>
        /// Tests exporting a function table that was imported.
        /// </summary>
        [TestMethod]
        public void Compile_TableImport_ExportedImport()
        {
            var module = new Module();
            module.Imports.Add(new Import.Table
            {
                Module = "Test",
                Field = "Test",
                Definition = new Table
                {
                    ElementType = ElementType.AnyFunction,
                    ResizableLimits = new ResizableLimits(1)
                }
            });
            module.Exports.Add(new Export
            {
                Name = nameof(ExportedTable.Table),
                Kind = ExternalKind.Table,
            });

            var table = new FunctionTable(0);

            var exportedTable = module.ToInstance<ExportedTable>(
                new ImportDictionary {
                    { "Test", "Test", table },
                })
                .Exports
                .Table;

            Assert.AreSame(table, exportedTable);
        }
    }
}