﻿using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.Sheets;

namespace CollectableCalculator;

// from https://github.com/awgil/ffxiv_satisfy
public unsafe class Calculations(IDataManager dataManager)
{
    // see Client::Game::SatisfactionSupplyManager.setCurrentNpc
    public int CalculateBonusGuarantee()
    {
        var framework = Framework.Instance();
        var proxy = framework->IsNetworkModuleInitialized ? framework->NetworkModuleProxy : null;
        var module = proxy != null ? proxy->NetworkModule : null;
        if (module == null)
            return -1;
        var timestamp = module->CurrentDeviceTime;
        timestamp += SatisfactionSupplyManager.Instance()->TimeAdjustmentForBonusGuarantee;
        return CalculateBonusGuarantee(timestamp);
    }

    // see getBonusGuaranteeIndex
    public int CalculateBonusGuarantee(int timestamp)
    {
        var secondsSinceStart = timestamp - 1657008000;
        var weeksSinceStart = secondsSinceStart / 604800;
        return weeksSinceStart % dataManager.GetExcelSheet<SatisfactionBonusGuarantee>().Count;
    }

    public uint[] CalculateRequestedItems(int npcIndex)
    {
        var inst = SatisfactionSupplyManager.Instance();
        var rank = inst->SatisfactionRanks[npcIndex];
        var supplyIndex = dataManager.GetExcelSheet<SatisfactionNpc>().GetRowOrDefault((uint)npcIndex + 1)!.Value
            .SatisfactionNpcParams[rank].SupplyIndex;
        return CalculateRequestedItems((uint)supplyIndex, inst->SupplySeed);
    }

    // see Client::Game::SatisfactionSupplyManager.onSatisfactionSupplyRead
    public uint[] CalculateRequestedItems(uint supplyIndex, uint seed)
    {
        var subrows = dataManager.GetSubrowExcelSheet<SatisfactionSupply>().GetRowOrDefault(supplyIndex)!.Value;

        var h1 = (0x03CEA65Cu * supplyIndex) ^ (0x1A0DD20Eu * seed);
        var h2 = (0xDF585D5Du * supplyIndex) ^ (0x3057656Eu * seed);
        var h3 = (0xED69E442u * supplyIndex) ^ (0x2202EA5Au * seed);
        var h4 = (0xAEFC3901u * supplyIndex) ^ (0xE70723F6u * seed);
        uint[] res = [0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF];
        var h5 = h1;
        for (int iSlot = 1; iSlot < 4; ++iSlot)
        {
            var sumProbabilities = 0;
            for (int iSub = 0; iSub < subrows.Count; ++iSub)
            {
                var row = subrows[iSub];
                if (row.Slot == iSlot)
                    sumProbabilities += row.ProbabilityPercent;
            }

            var hTemp = h5 ^ (h5 << 11);
            h1 = h3;
            h3 = h4;
            h5 = h2;
            h4 ^= hTemp ^ ((hTemp ^ (h4 >> 11)) >> 8);
            h2 = h1;

            var roll = h4 % sumProbabilities;
            for (int iSub = 0; iSub < subrows.Count; ++iSub)
            {
                var row = subrows[iSub];
                if (row.Slot != iSlot)
                    continue;
                if (roll < row.ProbabilityPercent)
                {
                    res[iSlot - 1] = (uint)iSub;
                    break;
                }

                roll -= row.ProbabilityPercent;
            }
        }

        return res;
    }
}
