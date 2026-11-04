# Input-only preparation; numerical values and row order are preserved.
combine_objective_phases <- function() {
  # 反力・把持力は20 Hz LPF後、速度は絶対値の時間平均が入力される。
  # 各往復: 停止を除く押し込み・戻しの時間加重平均。試行: 2往復の等重み平均。
  phases <- read_input("objective_phase_means.csv")
  stopifnot(nrow(phases) == 214L * 4L * 3L,
            !anyDuplicated(phases[c("subject", "set", "trial", "metric", "cycle", "direction")]))
  keys <- paste(trial_key(phases), phases$metric, sep = "/")
  rows <- lapply(unique(keys), function(key) {
    current <- phases[keys == key, ]
    stopifnot(nrow(current) == 4L, all(sort(current$cycle) == c(1, 1, 2, 2)))
    values <- vapply(1:2, function(cycle) {
      phase <- current[current$cycle == cycle, ]
      stopifnot(setequal(phase$direction, c("push", "return")))
      valid <- all(phase$status == "ok" & is.finite(phase$value) &
                     is.finite(phase$active_time_s) & phase$active_time_s > 0)
      if (!valid) return(NA_real_)
      sum(phase$value * phase$active_time_s) / sum(phase$active_time_s)
    }, numeric(1))
    result <- current[1, c("subject", "set", "trial", "stiffness", "vibration", "metric")]
    result$value <- mean(values)  # 一方の往復が利用不能なら試行値もNA。
    result$n_cycles <- sum(is.finite(values))
    result
  })
  do.call(rbind, rows)
}

objective_factors <- function(data) {
  data$subject <- factor(data$subject)
  data$set <- factor(data$set, levels = 1:3)
  data$stiffness <- factor(data$stiffness, levels = c(.1, .125, .15), labels = c("Low", "Mid", "High"))
  # 客観指標の入力: Non=0, Vib=1。主観データの1/2とは異なる。
  stopifnot(all(data$vibration %in% 0:1),
            !anyNA(data[c("subject", "set", "stiffness", "vibration", "value")]),
            nlevels(data$subject) == 12L, !anyDuplicated(trial_key(data)))
  data
}

read_subjective <- function() {
  raw <- read_input("subjective_trials.csv")
  columns <- c("参加者", "セット", "試行", "剛性番号", "振動番号",
               "質問番号", "評定[1-7]", "除外フラグ")
  stopifnot(all(columns %in% names(raw)))
  data <- raw[columns]
  names(data) <- c("subject", "set", "trial", "stiffness", "vibration",
                   "question", "rating", "excluded")
  stopifnot(nrow(data) == 648L, !anyNA(data),
            !anyDuplicated(data[c("subject", "set", "trial", "question")]),
            all(data$rating %in% 1:7), all(data$question %in% c("Q1", "Q2", "Q3")),
            all(data$stiffness %in% 2:4), all(data$vibration %in% 1:2),
            all(data$set %in% 1:3), all(data$trial %in% 1:6),
            all(tolower(data$excluded) %in% c("true", "false")))
  data$excluded <- tolower(data$excluded) == "true"
  # Exclusions are data, not identities: require two complete trials, shared
  # across all three questions. No participant ID is embedded in the code.
  excluded_by_trial <- split(data, trial_key(data))
  consistent <- vapply(excluded_by_trial, function(trial) {
    length(unique(trial$excluded)) == 1L &&
      setequal(trial$question, c("Q1", "Q2", "Q3"))
  }, logical(1))
  stopifnot(all(consistent), sum(data$excluded) == 6L,
            length(unique(trial_key(data[data$excluded, ]))) == 2L)
  data <- data[!data$excluded, ]
  data <- data[order(data$subject, data$set, data$trial), ]
  data$subject <- factor(data$subject)
  data$stiffness <- factor(data$stiffness, levels = 2:4)  # Low, Mid, High
  data$vibration <- factor(data$vibration, levels = 1:2)
  data$set <- factor(data$set, levels = 1:3)
  data$vc <- ifelse(data$vibration == "2", 0.5, -0.5)
  data$rating <- ordered(data$rating, levels = 1:7)
  data
}

