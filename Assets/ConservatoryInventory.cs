using System;
using UnityEngine;

// Conservatory-local supplies. No external module or debug grants are required.
[Serializable]
public sealed class ConservatoryInventory {
    public const int Capacity = 99;
    [SerializeField] private int seeds, water, organic, nectar, rareSeeds, produce;
    public int Seeds => seeds;
    public int Water => water;
    public int Organic => organic;
    public int Nectar => nectar;
    public int RareSeeds => rareSeeds;
    public int Produce => produce;
    public int AccruedProduce { get; private set; }
    public enum Resource { Seed, Water, Organic, Nectar, RareSeed, Produce }
    public int Count(Resource resource) {
        switch(resource) {
            case Resource.Seed: return seeds;
            case Resource.Water: return water;
            case Resource.Organic: return organic;
            case Resource.Nectar: return nectar;
            case Resource.RareSeed: return rareSeeds;
            default: return produce;
        }
    }
    public bool Spend(Resource resource, int amount) {
        if (amount <= 0 || Count(resource) < amount) return false;
        Set(resource, Count(resource) - amount); return true;
    }
    public int Add(Resource resource, int amount) {
        int accepted = Mathf.Clamp(amount, 0, Capacity - Count(resource));
        Set(resource, Count(resource) + accepted);
        if (resource == Resource.Produce) AccruedProduce += accepted;
        return accepted;
    }
    private void Set(Resource resource, int value) {
        switch(resource) {
            case Resource.Seed: seeds=value; break;
            case Resource.Water: water=value; break;
            case Resource.Organic: organic=value; break;
            case Resource.Nectar: nectar=value; break;
            case Resource.RareSeed: rareSeeds=value; break;
            default: produce=value; break;
        }
    }
    public bool ExchangeProduce() {
        if (produce < 1 || (seeds == Capacity && water == Capacity && organic == Capacity)) return false;
        Spend(Resource.Produce,1); Add(Resource.Seed,2); Add(Resource.Water,4); Add(Resource.Organic,2); return true;
    }
    public bool BuyRareSeed() {
        if (nectar < 1 || rareSeeds == Capacity) return false;
        Spend(Resource.Nectar,1); Add(Resource.RareSeed,1); return true;
    }
    public override string ToString() => $"Seeds {seeds}   Water {water}   Compost {organic}\nNectar {nectar}   Rare Seeds {rareSeeds}   Produce {produce}";
}
