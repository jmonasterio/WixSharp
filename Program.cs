using System;
using System.Collections.Generic;
using System.IO;

// Main at bottom show how to call this kind of library. Just a prototype.

namespace WixSharp
{

    public static class Constants
    {
        public static readonly WxGuid ProductGuid = new WxGuid();
    }

    // My idea was that I could use this type to constrain which nodes can have specific types of children.
    public interface IChildOf<T>
    {
    }

    public class WixTag
    {
    }

    public class WixLeafTag : WixTag
    {
        // Leaf's can't have children. This type prevents you from call EmitChildren.
    }

    public class WixNodeTag : WixTag
    {
        public WixNodeTag() 
        {
            Children = new List<WixTag>();
        }

        public List<WixTag> Children { get; set; }

        public WixRawXml Add(WixRawXml raw)
        {
            Children.Add(raw);
            return raw;
        }
    }


    public class WixDoc : WixNodeTag
    {
        public WixProduct Add(WixProduct product)
        {
            Children.Add(product);
            return product;
        }


    }

    public class WxGuid
    {
    }

    public class WixRawXml : WixTag
    {
        public string InnerXml { get; set; }
    }

    public class WixProduct : WixNodeTag, IChildOf<WixDoc>
    {
        // TBD: Need reasonable defaults.

        public WxGuid Id { get; set; }

        public string Language { get; set; }

        public string Manufacturer { get; set; }

        public string Name { get; set; }

        public string UpgradeCode { get; set; }

        public string Version { get; set; }

        public WixPackage Add(WixPackage wixPackage)
        {
            Children.Add(wixPackage);
            return wixPackage;
        }
    }

    public class WixPackage : WixLeafTag, IChildOf<WixProduct>
    {
        public string Compressed { get; set; }
        public string InstallerVersion { get; set; }

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

        public class EmitContext
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

        public static void Emit(this WixRawXml rawXml, EmitContext ctx)
        {
            ctx.Writer.Write(rawXml.InnerXml);
        }

        public static void Emit(this WixProduct product, EmitContext ctx)
        {
            ctx.Writer.Write("<product >");

            EmitSelfChildren(product, ctx);

            ctx.Writer.Write("</product >");
        }

        public static void Emit(this WixPackage package, EmitContext ctx)
        {
            ctx.Writer.Write("<package >");

            // Can't have children -- typesafe.
            //EmitSelfChildren( package, ctx);

            ctx.Writer.Write("</package >");
        }

        public static void Emit(this WixDoc doc, EmitContext ctx)
        {
            ctx.Writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            ctx.Writer.Write("<Wix xmlns=\"http://schemas.microsoft.com/wix/2006/wi\">");

             EmitSelfChildren(doc, ctx);

             ctx.Writer.Write("</Wix>");
        }


        public static void EmitSelfChildren(this WixNodeTag node, EmitContext ctx)
        {
            var oldParent = ctx.ParentTag;
            ctx.ParentTag = node;

            if (node.Children != null)
            {
                foreach (var child in node.Children)
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

        private static void Main(string[] args)
        {
            var wix = new WixDoc();
            var product = wix.Add(new WixProduct() {Id = Constants.ProductGuid, Language = "1033"}); // Yucky constructor.
            var package = product.Add(new WixPackage() {});
            product.Add(new WixRawXml() { InnerXml = @"<test />" });

            wix.EmitFile(new FileInfo(@"c:\temp\test.wxs"));


        }
    }


}
