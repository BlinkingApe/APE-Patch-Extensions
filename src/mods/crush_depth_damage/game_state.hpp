#ifndef APE_MODS_CRUSH_DEPTH_DAMAGE_GAME_STATE_HPP
#define APE_MODS_CRUSH_DEPTH_DAMAGE_GAME_STATE_HPP

// The seam between APE's damage loop and live OAE sh3.exe memory.
//
// The loop (damage_loop.{hpp,cpp}) depends only on this interface: it resolves
// the anchors once, then samples the four live values each iteration and writes
// hull HP back. The concrete implementation that AOB-scans stock sh3.exe and
// follows the recorded pointer chains is issue 01's deliverable; until those
// chains are resolved it is stubbed by UnresolvedGameState, which reports "not
// resolved" so the loop fails inert (applies no damage) rather than guessing.
//
// Keeping this an interface means the loop's timing / plausibility / band-apply
// logic is complete and reviewable now, and issue 01 plugs in a single class
// without touching the loop. See .scratch/crush-depth-damage/issues/01 and 05.

#include <string>

namespace ape {

// One iteration's snapshot of the live values the loop needs. Units match the
// engine: depth and crush_depth in metres, hull_hp in engine HP, mission_clock
// in game-seconds (monotonic; frozen while paused; scaled by time compression).
struct Readings {
  double depth = 0.0;
  double crush_depth = 0.0;
  double hull_hp = 0.0;
  double mission_clock = 0.0;
};

// All memory access happens through this interface, on the loop thread, never
// under the DLL loader lock. Every call reports a human-readable reason on
// failure so the loop can log why it is failing inert.
class IGameState {
 public:
  virtual ~IGameState() = default;

  // AOB-resolve every anchor / pointer-chain root once. Returns false (with a
  // reason) if any anchor scan misses — the loop then fails inert. May be
  // retried on later iterations (e.g. if the player boat is not spawned yet).
  virtual bool resolve(std::string& why) = 0;

  // True once resolve() has succeeded and the handles are usable.
  virtual bool resolved() const = 0;

  // Read all four live values for this iteration. Returns false (with a reason)
  // if any chain read faults or a handle is stale.
  virtual bool sample(Readings& out, std::string& why) = 0;

  // Write hull HP back to [boat + 0xB8] (plain read-modify-write; an aligned
  // float store is atomic on x86 — see PRD concurrency note). Returns false
  // (with a reason) on a write fault.
  virtual bool write_hull_hp(double hull_hp, std::string& why) = 0;
};

// Placeholder until issue 01 lands the real resolver. Never resolves, so the
// loop runs but always fails inert. This is the correct fail-safe behavior for
// a build whose anchors are not yet wired.
class UnresolvedGameState : public IGameState {
 public:
  bool resolve(std::string& why) override {
    why = "live-value anchors not yet wired (issue 01 pending)";
    return false;
  }
  bool resolved() const override { return false; }
  bool sample(Readings&, std::string& why) override {
    why = "not resolved";
    return false;
  }
  bool write_hull_hp(double, std::string& why) override {
    why = "not resolved";
    return false;
  }
};

}  // namespace ape

#endif  // APE_MODS_CRUSH_DEPTH_DAMAGE_GAME_STATE_HPP
