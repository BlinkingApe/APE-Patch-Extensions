#ifndef APE_CORE_LOG_HPP
#define APE_CORE_LOG_HPP

// Minimal append-only debug logger for ape.act.
//
// Not on the automated-test seam (PRD Testing Decisions): the loop is verified
// by cross-checking this log against expected arithmetic while forcing the
// submarine to test depths. Kept tiny and dependency-free on purpose.

#include <cstdio>
#include <mutex>
#include <string>

namespace ape {

class Logger {
 public:
  // Opens `path` for appending. If it cannot be opened, the logger silently
  // becomes a no-op (logging must never take the game down).
  explicit Logger(const std::string& path) {
    file_ = std::fopen(path.c_str(), "a");
  }
  ~Logger() {
    if (file_) std::fclose(file_);
  }

  Logger(const Logger&) = delete;
  Logger& operator=(const Logger&) = delete;

  bool ok() const { return file_ != nullptr; }

  // printf-style line; a newline is appended. Thread-safe.
  template <typename... Args>
  void logf(const char* fmt, Args... args) {
    if (!file_) return;
    std::lock_guard<std::mutex> lock(mutex_);
    std::fprintf(file_, fmt, args...);
    std::fputc('\n', file_);
    std::fflush(file_);
  }

 private:
  std::FILE* file_ = nullptr;
  std::mutex mutex_;
};

}  // namespace ape

#endif  // APE_CORE_LOG_HPP
