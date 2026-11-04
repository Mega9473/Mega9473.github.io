# 反力・把持力・速度・xmax: value ~ stiffness * vibration + set + (1 | subject)
# RMS: Vibのみ、value ~ stiffness + set + (1 | subject)
# REML、Satterthwaite自由度。主効果・交互作用のp値は未補正。

fit_objective <- function(data, formula) {
  control <- lmerControl(optimizer = "bobyqa", optCtrl = list(maxfun = 200000), calc.derivs = TRUE)
  fit_record(lmerTest::lmer(formula, data = data, REML = TRUE, control = control, na.action = na.fail))
}

# 指定した係数群が同時に0かを検定する。
objective_joint_test <- function(fit, pattern) {
  indices <- grep(pattern, names(fixef(fit)))
  stopifnot(length(indices) == 2L)
  L <- matrix(0, nrow = 2, ncol = length(fixef(fit)))
  L[cbind(1:2, indices)] <- 1
  as.data.frame(contestMD(fit, L, ddf = "Satterthwaite"))
}

# 剛性3条件とSetを等重み平均したVib=Nonの1自由度F検定。
# 同じ対比のt検定とF=t^2、自由度、p値が一致することを確認する。
objective_vibration_test <- function(fit) {
  emm <- emmeans(fit, ~ vibration, at = list(vibration = c(0, 1)),
                 weights = "equal", lmer.df = "satterthwaite")
  stopifnot(identical(as.numeric(emm@grid$vibration), c(0, 1)))
  basis <- contrast(emm, list(Vib_minus_Non = c(-1, 1)), adjust = "none")
  L <- basis@linfct
  stopifnot(qr(L)$rank == 1L, ncol(L) == length(fixef(fit)))
  joint <- as.data.frame(contestMD(fit, L, ddf = "Satterthwaite"))
  diagnostic <- as.data.frame(summary(basis, infer = c(TRUE, TRUE),
                                      level = .95, adjust = "none"))
  stopifnot(joint$NumDF == 1L,
            isTRUE(all.equal(joint$DenDF, diagnostic$df, tolerance = 1e-8)),
            isTRUE(all.equal(joint[["F value"]], diagnostic$t.ratio^2, tolerance = 1e-10)),
            isTRUE(all.equal(joint[["Pr(>F)"]], diagnostic$p.value, tolerance = 1e-10)))
  list(emm = emm, joint = joint)
}

# Vib・NonとSetを等重み平均したLow=Mid=Highの全体F検定。
# 2対比は全体検定の基底であり、個別の対比較検定は行わない。
objective_stiffness_test <- function(fit) {
  emm <- emmeans(fit, ~ stiffness, at = list(vibration = c(0, 1)),
                 weights = "equal", lmer.df = "satterthwaite")
  stopifnot(identical(as.character(emm@grid$stiffness), c("Low", "Mid", "High")))
  basis <- contrast(emm, list(Mid_minus_Low = c(-1, 1, 0),
                              High_minus_Low = c(-1, 0, 1)), adjust = "none")
  L <- basis@linfct
  stopifnot(qr(L)$rank == 2L, ncol(L) == length(fixef(fit)))
  joint <- as.data.frame(contestMD(fit, L, ddf = "Satterthwaite"))
  stopifnot(joint$NumDF == 2L)
  list(emm = emm, joint = joint)
}

objective_diagnostics <- function(record, metric) {
  fit <- record$model
  data.frame(metric, formula = paste(deparse(formula(fit)), collapse = " "),
             n_trials = nobs(fit), optimizer_code = fit@optinfo$conv$opt,
             singular = isSingular(fit, tol = 1e-4),
             convergence_messages = paste(fit@optinfo$conv$lme4$messages, collapse = " | "),
             warnings = paste(record$warnings, collapse = " | "),
             messages = paste(record$messages, collapse = " | "))
}

run_objective <- function() {
  data <- combine_objective_phases()
  # xmax入力: 押し込み開始～戻し終了(折返し停止を含む)の各往復最大位置の2往復平均。
  # 元エンコーダ位置[mm]。フィルタ・原点の再設定・300 mmでの打切りは行っていない。
  xmax <- read_input("xmax_trials.csv")
  xmax$value[xmax$status != "ok"] <- NA_real_
  data <- rbind(data, xmax[names(data)])
  write_result(data, "objective_trial_values")
  units <- c(reaction = "N", grip = "N", speed = "mm/s", xmax = "mm")
  models <- tests <- posthoc <- emms <- diagnostics <- residuals_all <- list()
  stiffness_tests <- stiffness_emms <- list()
  cohort <- NULL

  for (metric in names(units)) {
    current <- data[data$metric == metric & is.finite(data$value), ]
    stopifnot(nrow(current) == 202L, all(current$n_cycles == 2L))
    if (is.null(cohort)) cohort <- trial_key(current)
    stopifnot(setequal(cohort, trial_key(current)))
    current <- objective_factors(current)
    record <- fit_objective(current, value ~ stiffness * vibration + set + (1 | subject))
    fit <- record$model
    models[[metric]] <- record
    diagnostics[[metric]] <- objective_diagnostics(record, metric)
    residuals_all[[metric]] <- data.frame(metric, current[c("subject", "set", "trial")],
                                        fitted = fitted(fit), residual = residuals(fit))

    vibration <- objective_vibration_test(fit)
    vibration_joint <- vibration$joint
    average <- contrast_summary(vibration$emm, vibration_contrast)
    joint <- objective_joint_test(fit, "stiffness.*:vibration|vibration:stiffness")
    stiffness <- objective_stiffness_test(fit)
    stiffness_joint <- stiffness$joint
    stiffness_tests[[metric]] <- data.frame(
      metric, unit = units[[metric]], n_trials = nobs(fit),
      n_subjects = nlevels(current$subject), effect = "Stiffness", test = "F",
      F = stiffness_joint[["F value"]], df1 = stiffness_joint$NumDF,
      df2 = stiffness_joint$DenDF, p_raw = stiffness_joint[["Pr(>F)"]]
    )
    stiffness_emms[[metric]] <- data.frame(
      metric, unit = units[[metric]],
      as.data.frame(confint(stiffness$emm, level = .95, adjust = "none"))
    )
    tests[[metric]] <- data.frame(
      metric, unit = units[[metric]], estimate = average$estimate, SE = average$SE,
      lower95 = average$lower.CL, upper95 = average$upper.CL,
      vibration_F = vibration_joint[["F value"]], vibration_df1 = vibration_joint$NumDF,
      vibration_df2 = vibration_joint$DenDF, p_raw = vibration_joint[["Pr(>F)"]],
      t_diagnostic = average$t.ratio, t_df_diagnostic = average$df,
      t_p_raw_diagnostic = average$p_raw,
      interaction_F = joint[["F value"]], interaction_df1 = joint$NumDF,
      interaction_df2 = joint$DenDF, interaction_p_raw = joint[["Pr(>F)"]],
      stiffness_F = stiffness_joint[["F value"]], stiffness_df1 = stiffness_joint$NumDF,
      stiffness_df2 = stiffness_joint$DenDF, stiffness_p_raw = stiffness_joint[["Pr(>F)"]]
    )
    condition_emm <- emmeans(fit, ~ stiffness * vibration, at = list(vibration = c(0, 1)),
                             weights = "equal", lmer.df = "satterthwaite")
    emms[[metric]] <- data.frame(metric, as.data.frame(confint(condition_emm, level = .95, adjust = "none")))

    # 交互作用が有意な指標のみ、3剛性のVib−Non比較を行い、指標内でHolm補正。
    if (joint[["Pr(>F)"]] < alpha) {
      simple <- emmeans(fit, ~ vibration | stiffness, at = list(vibration = c(0, 1)),
                         weights = "equal", lmer.df = "satterthwaite")
      posthoc[[metric]] <- data.frame(metric, contrast_summary(simple, vibration_contrast, holm = TRUE))
    }
    cat(metric, " LMM 完了\n")
  }

  # RMS入力: 各往復の時間加重平均を除去した時間加重RMS。バンドパスなし。
  rms <- read_input("rms_trials.csv")
  current <- rms[rms$vibration == 1 & is.finite(rms$value), ]
  stopifnot(nrow(current) == 100L, all(current$n_cycles == 2L),
            all(is.finite(current$cycle1_RMS)), all(is.finite(current$cycle2_RMS)),
            max(abs(current$value - (current$cycle1_RMS + current$cycle2_RMS) / 2)) < 1e-10)
  current$value <- (current$cycle1_RMS + current$cycle2_RMS) / 2
  current <- objective_factors(current)
  record <- fit_objective(current, value ~ stiffness + set + (1 | subject))
  fit <- record$model
  models$RMS <- record
  diagnostics$RMS <- objective_diagnostics(record, "RMS")
  residuals_all$RMS <- data.frame(metric = "RMS", current[c("subject", "set", "trial")],
                                fitted = fitted(fit), residual = residuals(fit))
  joint <- objective_joint_test(fit, "^stiffness")
  rms_emm <- emmeans(fit, ~ stiffness, weights = "equal", lmer.df = "satterthwaite")
  write_result(data.frame(F = joint[["F value"]], df1 = joint$NumDF, df2 = joint$DenDF,
                          p_raw = joint[["Pr(>F)"]]), "RMS_omnibus")
  write_result(as.data.frame(confint(rms_emm, level = .95, adjust = "none")), "RMS_EMMs")
  if (joint[["Pr(>F)"]] < alpha) {
    write_result(contrast_summary(rms_emm, stiffness_contrasts, holm = TRUE), "RMS_posthoc")
  }
  # 現在のRMS図はEMMではなく、参加者内平均の参加者間平均±SD。
  participant_means <- aggregate(value ~ subject + stiffness, current, mean)
  rms_summary <- do.call(rbind, lapply(levels(current$stiffness), function(stiffness) {
    values <- participant_means$value[participant_means$stiffness == stiffness]
    data.frame(stiffness, n_subjects = length(values), mean_RMS = mean(values), SD = sd(values))
  }))
  write_result(rms_summary, "RMS_descriptive")

  write_result(do.call(rbind, tests), "objective_tests")
  write_result(do.call(rbind, stiffness_tests), "objective_stiffness_tests")
  write_result(do.call(rbind, stiffness_emms), "objective_stiffness_EMMs")
  if (length(posthoc)) write_result(do.call(rbind, posthoc), "objective_posthoc")
  write_result(do.call(rbind, emms), "objective_EMMs")
  write_result(do.call(rbind, diagnostics), "objective_diagnostics")
  write_result(do.call(rbind, residuals_all), "objective_residuals")
  saveRDS(models, file.path(output_dir, "objective_models.rds"))
  cat("RMS LMM 完了\n")
}
