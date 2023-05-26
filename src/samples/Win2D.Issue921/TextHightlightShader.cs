using System;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct2D1;
using Vortice.Direct3D;

namespace Vortice.Windows.Discussion400;

class TextHightlightShader : ID2D1Effect {
    public static void Register(ID2D1Factory1 d2Factory) => d2Factory.RegisterEffect<Implementation>();

    public TextHightlightShader(ID2D1DeviceContext context) : base(context.CreateEffect(typeof(Implementation).GUID)) {}
    public TextHightlightShader(ID2D1EffectContext context) : base(context.CreateEffect(typeof(Implementation).GUID)) {}

    [CustomEffect(1)]
    class Implementation : CustomEffectBase, ID2D1EffectImpl {
        ShaderTransform? ShaderT;

        static readonly System.Collections.Generic.HashSet<Implementation> all = new(); // https://github.com/amerkoleci/Vortice.Windows/issues/315
        public Implementation() => all.Add(this);                                       // https://github.com/amerkoleci/Vortice.Windows/issues/315

        protected override void DisposeCore(bool disposing) {
            if (disposing) {
                ShaderT?.Dispose();
                ShaderT = null;

                all.Remove(this);                                                       // https://github.com/amerkoleci/Vortice.Windows/issues/315
            }

            base.DisposeCore(disposing);
        }

        public override void Initialize(ID2D1EffectContext effectContext, ID2D1TransformGraph transformGraph) {
            // https://learn.microsoft.com/en-us/windows/win32/direct2d/custom-effects#initializeid2d1effectcontext-pcontextinternal-id2d1transformgraph-ptransformgraph
            //
            // > Direct2D calls the Initialize method after the ID2D1DeviceContext::CreateEffect method has been called by the app.
            // > You can use this method to perform internal initialization or any other operations needed for the effect.
            // > Additionally, you can use it to create the effect's initial transform graph.
            base.Initialize(effectContext, transformGraph);

            ShaderT = new ShaderTransform(effectContext);

            transformGraph.AddNode(ShaderT);
            transformGraph.SetSingleTransformNode(ShaderT);
        }

        public override void SetGraph(ID2D1TransformGraph transformGraph) {
            // https://learn.microsoft.com/en-us/windows/win32/direct2d/custom-effects#setgraphid2d1transformgraph-ptransformgraph
            //
            // > Direct2D calls the SetGraph method when the number of inputs to the effect is changed.
            // > While most effects have a constant number of inputs, others like the Composite effect support a variable number of inputs.
            // > This method allows these effects to update their transform graph in response to a changing input count.
            base.SetGraph(transformGraph);
        }

        public override void PrepareForRender(ChangeType changeType) {
            // https://learn.microsoft.com/en-us/windows/win32/direct2d/custom-effects#prepareforrender-d2d1_change_type-changetype
            //
            // > The PrepareForRender method provides an opportunity for effects to perform any operations in response to external changes.
            // > Direct2D calls this method just before it renders an effect if at least one of these is true:
            // >
            // > - The effect has been previously initialized but not yet drawn.
            // > - An effect property has changed since the last draw call.
            // > - The state of the calling Direct2D context(like DPI) has changed since the last draw call.
            base.PrepareForRender(changeType);
        }
    }

    class ShaderTransform : CallbackBase, ID2D1DrawTransform {
        public ShaderTransform(ID2D1EffectContext effectContext) {
            var compilation = Compiler.Compile(
                shaderSource: @"
                    float4 main() : SV_TARGET {
                        return float4(0.1f, 0.2f, 0.3f, 0.4f);
                    }
                ",
                defines: Array.Empty<ShaderMacro>(),
                include: null!,
                entryPoint: "main",
                sourceName: "TextHightlight.hlsl",
                profile: "ps_5_0",
                shaderFlags: ShaderFlags.Debug,
                effectFlags: EffectFlags.None,
                out var blob,
                out var errorBlob
            );

            using (blob)
            using (errorBlob) {
                compilation.CheckError();

                var bytes = blob.AsBytes();
                effectContext.LoadPixelShader(typeof(ShaderTransform).GUID, bytes, bytes.Length);
            }
        }

        public int GetInputCount() => 1;

        public void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect) {
            outputRect = inputRects[0];
            outputOpaqueSubRect = inputOpaqueSubRects[0];
        }

        public void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects) {
            inputRects[0] = outputRect;
        }

        public RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect) {
            return invalidInputRect;
        }

        public void SetDrawInfo(ID2D1DrawInfo drawInfo) {
            drawInfo.SetPixelShader(typeof(ShaderTransform).GUID, PixelOptions.None);
        }
    }
}
