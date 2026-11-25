// h.sie's clock cave, added with depth stress (depth-pressure-stress ticket 36).
//
// ape.act reads the mission clock from 0x0063FF80, a mirror that h.sie's
// clock cave writes: at 0x004C2115 stock's `mov [esi+0x150],eax` becomes a jump
// to 0x0051CFC7, which stores the clock to the mirror, does the stock store and
// jumps back. ape.act finds the mirror by that cave's bytes (its 'clock' anchor).
// Most h.sie installs' sh3.exe carry the cave (OAE 1.5.2's does); GWX-KC 2.5's
// (e2b190e1) does not, so there the anchor never resolves and depth stress does
// nothing.
//
// With depth stress, APE writes h.sie's cave bytes into an exe that has stock's store at
// 0x004C2115, zero slack at 0x0051CFC7, and no match for the clock signature
// anywhere. It keeps sh3.exe as it was in sh3.exe.ape-orig before its first
// write; that copy is also what marks the cave as APE's, since h.sie's own cave
// is byte-identical. Revert writes the two sites back and deletes the copy. An exe
// in any other state is never written.

using System;
using System.IO;

namespace ApePatcher
{
    public enum ClockCaveState
    {
        NotNeeded,          // the clock signature already resolves: h.sie's own cave
        Missing,            // stock store, empty slack, no signature: APE adds it with depth stress
        Applied,            // APE's cave, beside APE's copy of sh3.exe
        ChangedByOtherMod,  // APE's copy is there, but the sites no longer hold either state
        Unrecognised,       // no signature, but the sites hold something APE doesn't know
        Unreadable,         // APE's copy isn't the exe APE patched, or an I/O error
    }

    /// <summary>sh3.exe's clock cave, as read when the session opened.</summary>
    public class ClockCaveInfo
    {
        public ClockCaveState State = ClockCaveState.Unrecognised;
        public string Problem;

        /// <summary>sh3.exe.ape-orig as read, or null when there is none.</summary>
        public byte[] Original;

        /// <summary>How many times ape.act's clock signature matches the exe, and
        /// the mirror address the first match names (0 when none).</summary>
        public int ClockMatches;
        public uint ClockTarget;
    }

    public static class ClockCave
    {
        public const string PatchId = "clock-cave";
        public const string OrigSuffix = ".ape-orig";

        public const int SiteOffset = 0xC2115;      // VA 0x004C2115, stock's clock store
        public const int CaveOffset = 0x11CFC7;     // VA 0x0051CFC7, slack at the end of .text
        public const uint Mirror = 0x0063FF80;

        // mov [esi+0x150],eax
        static readonly byte[] StockStore = { 0x89, 0x86, 0x50, 0x01, 0x00, 0x00 };
        // jmp 0x0051CFC7; nop
        static readonly byte[] Jump = { 0xE9, 0xAD, 0xAE, 0x05, 0x00, 0x90 };
        // mov [0x63FF80],eax; mov [esi+0x150],eax; jmp 0x004C211B
        static readonly byte[] Cave = { 0xA3, 0x80, 0xFF, 0x63, 0x00, 0x89, 0x86, 0x50, 0x01, 0x00, 0x00,
                                        0xE9, 0x44, 0x51, 0xFA, 0xFF };

        // ape.act's clock anchor (src/ape_act/game_memory.cpp): A3 ?? ?? ?? ?? 89 86 50 01 00 00 E9.
        // -1 is a wildcard.
        static readonly int[] Signature = { 0xA3, -1, -1, -1, -1, 0x89, 0x86, 0x50, 0x01, 0x00, 0x00, 0xE9 };

        public static ClockCaveInfo Classify(byte[] exe, byte[] original)
        {
            ClockCaveInfo c = new ClockCaveInfo();
            c.Original = original;
            Scan(exe, c);

            if (original != null)
            {
                if (!Holds(original, SiteOffset, StockStore) || !Holds(original, CaveOffset, new byte[Cave.Length]))
                {
                    c.State = ClockCaveState.Unreadable;
                    c.Problem = "APE's own copy, " + PatchSession.Sh3ExeName + OrigSuffix
                        + ", is not the " + PatchSession.Sh3ExeName + " APE patched";
                    return c;
                }
                if (IsApplied(exe, c)) { c.State = ClockCaveState.Applied; return c; }
                // The stock store back beside a copy: a revert whose copy didn't
                // delete, or a tool that rewrote sh3.exe. The next apply reuses it.
                if (IsMissing(exe, c)) { c.State = ClockCaveState.Missing; return c; }
                c.State = ClockCaveState.ChangedByOtherMod;
                c.Problem = "it no longer holds the clock fix APE wrote";
                return c;
            }

            if (c.ClockMatches > 0) { c.State = ClockCaveState.NotNeeded; return c; }
            if (IsMissing(exe, c)) { c.State = ClockCaveState.Missing; return c; }
            c.State = ClockCaveState.Unrecognised;
            c.Problem = "its clock code is not the stock code APE knows how to extend";
            return c;
        }

        static bool IsApplied(byte[] exe, ClockCaveInfo c)
        {
            return Holds(exe, SiteOffset, Jump) && Holds(exe, CaveOffset, Cave)
                && c.ClockMatches == 1 && c.ClockTarget == Mirror;
        }

        static bool IsMissing(byte[] exe, ClockCaveInfo c)
        {
            return c.ClockMatches == 0 && Holds(exe, SiteOffset, StockStore)
                && Holds(exe, CaveOffset, new byte[Cave.Length]);
        }

        /// <summary>The exe with the cave written in.</summary>
        public static void WriteCave(byte[] exe)
        {
            Jump.CopyTo(exe, SiteOffset);
            Cave.CopyTo(exe, CaveOffset);
        }

        /// <summary>The exe with stock's store and the empty slack back.</summary>
        public static void WriteStock(byte[] exe)
        {
            StockStore.CopyTo(exe, SiteOffset);
            Array.Clear(exe, CaveOffset, Cave.Length);
        }

        /// <summary>Counts the clock signature over the whole file. ape.act scans
        /// only executable sections, so this counts at least as many matches: a
        /// match anywhere means APE leaves the exe alone.</summary>
        static void Scan(byte[] exe, ClockCaveInfo c)
        {
            for (int at = 0; at + Signature.Length <= exe.Length; at++)
            {
                if (exe[at] != 0xA3) continue;
                bool match = true;
                for (int i = 1; i < Signature.Length && match; i++)
                    if (Signature[i] >= 0 && exe[at + i] != Signature[i]) match = false;
                if (!match) continue;
                if (c.ClockMatches == 0) c.ClockTarget = BitConverter.ToUInt32(exe, at + 1);
                c.ClockMatches++;
            }
        }

        static bool Holds(byte[] file, int at, byte[] want)
        {
            if (at < 0 || at + want.Length > file.Length) return false;
            for (int i = 0; i < want.Length; i++) if (file[at + i] != want[i]) return false;
            return true;
        }
    }
}
