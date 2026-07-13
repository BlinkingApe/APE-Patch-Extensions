// APE patcher — standalone tool that inserts the dedicated ape.act LoadLibrary
// call site into a given OAE sh3.exe (ADR-0002, ADR-0005). Re-runnable against
// a freshly rebuilt sh3.exe.
//
// Scaffold: argument surface only. The actual patch (locate the call site,
// write the LoadLibrary("ape.act") trampoline, verify idempotency) lands under
// .scratch/crush-depth-damage/issues/06.

#include <cstdio>
#include <cstring>

namespace {

int usage(const char* argv0) {
  std::printf(
      "APE patcher\n"
      "Usage: %s <path-to-sh3.exe>\n"
      "\n"
      "Inserts the ape.act load call site into the given OAE sh3.exe.\n"
      "Safe to re-run after an OAE rebuild.\n",
      argv0);
  return 2;
}

}  // namespace

int main(int argc, char** argv) {
  if (argc != 2 || std::strcmp(argv[1], "-h") == 0 ||
      std::strcmp(argv[1], "--help") == 0) {
    return usage(argv[0]);
  }

  // TODO(issue-06): implement the sh3.exe patch.
  std::fprintf(stderr, "patcher: not yet implemented (target: %s)\n", argv[1]);
  return 1;
}
