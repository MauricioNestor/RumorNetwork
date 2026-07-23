using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Catalog
{
    public sealed class VerifiedDiscoveryChunkSafetyPatch
        : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.verified-discovery-chunk-safety";

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -1;
        }

        public override void StartServerSide(
            ICoreServerAPI api
        )
        {
            harmony = new Harmony(HarmonyId);

            var original = AccessTools.Method(
                typeof(VerifiedStructureDiscoveryService),
                "InspectTranslocatorBlocks"
            );

            if (original == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou " +
                    "InspectTranslocatorBlocks para aplicar " +
                    "a proteção de chunks temporários."
                );

                return;
            }

            harmony.Patch(
                original,
                prefix: new HarmonyMethod(
                    typeof(VerifiedDiscoveryChunkSafetyPatch),
                    nameof(PrepareChunkData)
                ),
                transpiler: new HarmonyMethod(
                    typeof(VerifiedDiscoveryChunkSafetyPatch),
                    nameof(ReplaceUnsafeContainsBlock)
                )
            );
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        private static void PrepareChunkData(
            IServerChunk[] chunks
        )
        {
            if (chunks == null)
            {
                return;
            }

            foreach (IServerChunk chunk in chunks)
            {
                if (
                    chunk == null ||
                    chunk.Empty ||
                    chunk.Disposed
                )
                {
                    continue;
                }

                try
                {
                    chunk.Unpack_ReadOnly();
                }
                catch (
                    Exception exception
                ) when (
                    exception is NullReferenceException ||
                    exception is ObjectDisposedException
                )
                {
                    // A coluna pode ser anunciada pelo evento antes de
                    // todas as camadas estarem materializadas. O método
                    // original continuará, mas SafeContainsBlock tratará
                    // essa camada como vazia em vez de derrubar o servidor.
                }
            }
        }

        private static IEnumerable<CodeInstruction>
            ReplaceUnsafeContainsBlock(
                IEnumerable<CodeInstruction> instructions
            )
        {
            var containsBlock = AccessTools.Method(
                typeof(IChunkBlocks),
                nameof(IChunkBlocks.ContainsBlock),
                new[] { typeof(int) }
            );

            var safeContainsBlock = AccessTools.Method(
                typeof(VerifiedDiscoveryChunkSafetyPatch),
                nameof(SafeContainsBlock)
            );

            if (
                containsBlock == null ||
                safeContainsBlock == null
            )
            {
                throw new InvalidOperationException(
                    "Não foi possível resolver ContainsBlock " +
                    "para a proteção do catálogo remoto."
                );
            }

            int replacementCount = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (!instruction.Calls(containsBlock))
                {
                    yield return instruction;
                    continue;
                }

                CodeInstruction replacement = new(
                    OpCodes.Call,
                    safeContainsBlock
                );

                replacement.labels.AddRange(
                    instruction.labels
                );

                replacement.blocks.AddRange(
                    instruction.blocks
                );

                replacementCount++;
                yield return replacement;
            }

            if (replacementCount == 0)
            {
                throw new InvalidOperationException(
                    "A chamada insegura a ContainsBlock não " +
                    "foi encontrada no catálogo remoto."
                );
            }
        }

        private static bool SafeContainsBlock(
            IChunkBlocks? data,
            int blockId
        )
        {
            if (data == null)
            {
                return false;
            }

            try
            {
                return data.ContainsBlock(blockId);
            }
            catch (
                Exception exception
            ) when (
                exception is NullReferenceException ||
                exception is ObjectDisposedException
            )
            {
                return false;
            }
        }
    }
}
