using System;
using System.Collections.Generic;
using System.IO;

// Main at bottom show how to call this kind of library. Just a prototype.

// Ideas: 
// - Use the Wix Documenation compiler to generate classes like WixProduct from XSD: https://github.com/wixtoolset/wix3/blob/develop/src/tools/wix/Xsd/wix.xsd
// - Add methods at end to compile/link.
// - Implement a guid hash for ID's, so hashes stay same as long as filename stays same.
// - Hash prefix.
// - Make an example the inherits for WixProduct for customization, like WixIisProduct.
// - Add a validate method to check each class against XSD to make sure it has all children filled in (generated code).
// - Generate DOM, emitter, and validator
// - Read a WIX file and generate DOM code.


namespace WixSharp
{

    public class WixTag
    {
        // Or null.
        public WixTag Parent { get; set; }
    }

    public class WixLeafTag : WixTag
    {
        public WixLeafTag() 
        {

        }

        // Leaf's can't have children. This type prevents you from call EmitChildren.
    }

    public class WixNodeTag : WixTag
    {
        public WixNodeTag()
        {
            Custom = new List<WixRawXml>();
        }

        public List<WixRawXml> Custom { get; set; }

        public WixRawXml Add(WixRawXml raw)
        {
            Custom.Add(raw);
            return raw;
        }
    }


    public class WixDoc : WixNodeTag
    {
        public WixProduct  Product { get; set; }
    }

    public class WxGuid
    {
        public WxGuid()
        {
            Value = Guid.NewGuid();
        }

        public Guid Value { get; set; }
    }

    public class WixRawXml : WixTag
    {
        public WixRawXml()
        {
        }

        public string InnerXml { get; set; }
    }

    public class WixProduct : WixNodeTag
    {
        // TBD: Need reasonable defaults.

        public WxGuid Id { get; set; }

        public string Language { get; set; }

        public string Manufacturer { get; set; }

        public string Name { get; set; }

        public string UpgradeCode { get; set; }

        public string Version { get; set; }


        public WixPackage Package { get; set; }
    }

    public class WixIisProduct : WixProduct
    {
        // I can imagine something like this, that has many settings preconfigured for IIS.
    }

    public class WixPackage : WixLeafTag
    {
        public string Compressed { get; set; }
        public string InstallerVersion { get; set; }
    }

    // TBD: Generated code (from xsd)
    public static class WixValidator
    {
        public static void Validate(this WixDoc doc)
        {
            var ctx = new ValidationContext(doc);
            Validate( doc, ctx);

            if (ctx.Errors.Count > 0)
            {
                throw new Exception( "One or more validation errors occurred.");
            }

        }

        public struct ValidationError
        {
            public WixTag TagWithError { get; set; }
            public WixTag ParentTagWithError { get; set; } // TBD: Could be a stack all the way to the top.
            public string Message { get; set; }
        }

        private class ValidationContext
        {
            public WixDoc Root { get; private set; }
            public WixNodeTag ParentTag { get; set; }
            public List<ValidationError> Errors { get; set; }

            public ValidationContext(WixDoc root)
            {
                Root = root;
                ParentTag = null;
                Errors = new List<ValidationError>();

            }
        }

        // TBD: Simple example. This code will all be genreated.
        private static void Validate(this WixDoc doc, ValidationContext ctx)
        {
            if (doc.Product == null)
            {
                ctx.Errors.Add(new ValidationError() {Message = "Missing product", TagWithError = doc, ParentTagWithError = doc.Parent});
            }
            // Drill down
            Validate( doc.Product, ctx);
        }

        private static void Validate(this WixProduct product, ValidationContext ctx)
        {
            if (product.Id == null)
            {
                // Missing ID
                ctx.Errors.Add(new ValidationError() { Message = "Missing product", TagWithError = product, ParentTagWithError = product.Parent });
            }
        }

    }


    public static class WixEmitter
    {
        public static void EmitFile(this WixDoc doc, FileInfo fileInfo)
        {
            using (var stream = fileInfo.CreateText())
            {
                var ctx = new EmitContext(doc, stream);
                Emit(doc, ctx);
            }
        }

        private class EmitContext
        {
            public WixDoc Root { get; private set; }
            public WixNodeTag ParentTag { get; set; }
            public StreamWriter Writer { get; private set; }

            public EmitContext(WixDoc root, StreamWriter sw)
            {
                Root = root;
                ParentTag = null;
                Writer = sw;
            }
        }

        private static void Emit(this WixRawXml rawXml, EmitContext ctx)
        {
            ctx.Writer.Write(rawXml.InnerXml);
        }

        private static void Emit(this WixProduct product, EmitContext ctx)
        {
            ctx.Writer.Write("<product >");

            Emit(product.Package, ctx);

            // This is a lame way to hang some other xml off a node.
            EmitSelfChildren(product, ctx);

            ctx.Writer.Write("</product >");
        }

        private static void Emit(this WixPackage package, EmitContext ctx)
        {
            ctx.Writer.Write("<package >");

            // Can't have children -- typesafe.
            //EmitSelfChildren( package, ctx);

            ctx.Writer.Write("</package >");
        }

        private static void Emit(this WixDoc doc, EmitContext ctx)
        {
            ctx.Writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            ctx.Writer.Write("<Wix xmlns=\"http://schemas.microsoft.com/wix/2006/wi\">");

             EmitSelfChildren(doc, ctx);

             ctx.Writer.Write("</Wix>");
        }


        private static void EmitSelfChildren(this WixNodeTag node, EmitContext ctx)
        {
            var oldParent = ctx.ParentTag;
            ctx.ParentTag = node;

            if (node.Custom != null)
            {
                foreach (var child in node.Custom)
                {
                    // Dynamic dispatch. Calls the correct Emit override in THIS class based on type.
                    Emit(((dynamic)child), ctx); // This can fail if you don't have an override for every time.
                }
            }


            ctx.ParentTag = oldParent;
        }

    }

    public static class Extensions
    {
        public static void With<T>(this T input, Action<T> action)
        {
            action(input);
        }

        public static void Set(this object obj, params Func<string, object>[] hash)
        {
            foreach (Func<string, object> member in hash)
            {
                var propertyName = member.Method.GetParameters()[0].Name;
                var propertyValue = member(string.Empty);
                obj.GetType()
                    .GetProperty(propertyName)
                    .SetValue(obj, propertyValue, null);
            }
            ;
        }
    }

    public class Program
    {
        public static class Settings
        {
            public static readonly WxGuid ProductGuid = new WxGuid();
            public static readonly string Language = "1033";
        }

        private static void Main(string[] args)
        {
            // Is this really any better than writing XML? I can do loops and stuff, but...
            var wix = new WixDoc()
            {
                Product = new WixProduct()
                {
                    Id = Settings.ProductGuid,
                    Language = Settings.Language,

                    Package = new WixPackage()
                    {
                    }
                }
            };

            // TBD. This may not be that useful. How do I guarantee correct order of output.
            wix.Product.Custom.Add(new WixRawXml() {InnerXml = @"<test />"});

            wix.Validate();
            wix.EmitFile(new FileInfo(@"c:\temp\test.wxs"));

            // TBD
            // wix.CompileAndLink(flags);

        }
    }


}
