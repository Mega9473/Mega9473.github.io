# Q1・Q2: rating ~ stiffness * vibration + set + (1 + vc | subject)
# stiffness * vibration = stiffness + vibration + stiffness:vibration
# 固定効果のvibrationは因子(1=Non, 2=Vib)。vcはNon=-0.5, Vib=+0.5。
# Q3: Vibのみ、rating ~ stiffness + set + (1 | subject)

fit_subjective <- function(data, formula, nAGQ) {
  control <- clmm.control(
    method = "nlminb", maxIter = 200, gradTol = 1e-6, maxLineIter = 100,
    iter.max = 1000, eval.max = 2000, rel.tol = 1e-10
  )
  fit_record(clmm(formula, data = data, link = "logit", threshold = "flexible",
                  nAGQ = nAGQ, Hess = TRUE, control = control, na.action = na.fail))
}

with_odds_ratio <- function(result) {
  result$OR <- exp(result$estimate)
  result$OR_lower95 <- exp(result$asymp.LCL)
  result$OR_upper95 <- exp(result$asymp.UCL)
  result
}

# 剛性の全体検定: 潜在尺度EMMの独立な2対比に対するWald χ²検定。
stiffness_wald <- function(emm) {
  contrasts <- contrast(emm, stiffness_contrasts[1:2], adjust = "none")
  difference <- as.data.frame(contrasts)$estimate
  statistic <- drop(crossprod(difference, solve(vcov(contrasts), difference)))
  list(statistic = statistic, p = pchisq(statistic, df = 2, lower.tail = FALSE))
}

# 図示: ランダム効果を0としたカテゴリ確率の積和 E[Y]=sum(k*P[Y=k])。
# 比較・図示しない固定因子は等重みで平均。CIは未補正95%。
expected_rating <- function(fit, data, by) {
  formula <- as.formula(paste("~ rating |", paste(by, collapse = " * ")))
  probabilities <- emmeans(fit, formula, mode = "prob", weights = "equal", data = data)
  scores <- as.numeric(as.character(unique(probabilities@grid$rating)))
  stopifnot(identical(scores, as.numeric(1:7)))
  result <- as.data.frame(summary(
    contrast(probabilities, list(expected_score = scores), by = by, adjust = "none"),
    infer = c(TRUE, FALSE), level = 0.95, adjust = "none"
  ))
  for (name in setdiff(c("stiffness", "vibration"), names(result))) result[[name]] <- "average"
  result
}

subjective_diagnostics <- function(record, id) {
  fit <- record$model
  data.frame(
    model = id, formula = paste(deparse(formula(fit)), collapse = " "),
    n_trials = nobs(fit), nAGQ = fit$nAGQ,
    convergence_code = fit$optRes$convergence,
    max_gradient = max(abs(fit$gradient)),
    min_hessian_eigenvalue = min(eigen(fit$Hessian, symmetric = TRUE, only.values = TRUE)$values),
    warnings = paste(record$warnings, collapse = " | "),
    messages = paste(record$messages, collapse = " | ")
  )
}

run_subjective <- function() {
  data <- read_subjective()
  models <- tests <- posthoc <- predictions <- diagnostics <- sets <- list()
  for (question in c("Q1", "Q2", "Q3")) {
    current <- data[data$question == question, ]
    if (question == "Q3") current <- current[current$vibration == "2", ]
    stopifnot(nrow(current) == if (question == "Q3") 106L else 214L,
              nlevels(current$subject) == 12L)

    # 固定効果・ランダム効果の構造と積分近似は現在の解析と同一。
    full <- if (question == "Q3") {
      fit_subjective(current, rating ~ stiffness + set + (1 | subject), nAGQ = 9L)
    } else {
      fit_subjective(current, rating ~ stiffness * vibration + set + (1 + vc | subject), nAGQ = 1L)
    }
    fit <- full$model
    models[[question]] <- list(full = full)
    diagnostics[[question]] <- subjective_diagnostics(full, question)

    stiffness_emm <- emmeans(fit, ~ stiffness, mode = "latent", weights = "equal", data = current)
    stiffness_test <- stiffness_wald(stiffness_emm)
    tests[[paste0(question, "_stiffness")]] <- data.frame(
      question, effect = "Stiffness", test = "Wald chi-square",
      statistic = stiffness_test$statistic, df = 2, estimate = NA_real_, SE = NA_real_,
      lower95 = NA_real_, upper95 = NA_real_, OR = NA_real_,
      OR_lower95 = NA_real_, OR_upper95 = NA_real_, p_raw = stiffness_test$p
    )

    interaction_p <- NA_real_
    if (question != "Q3") {
      vibration_emm <- emmeans(fit, ~ vibration, mode = "latent", weights = "equal", data = current)
      average <- with_odds_ratio(contrast_summary(vibration_emm, vibration_contrast))
      tests[[paste0(question, "_vibration")]] <- data.frame(
        question, effect = "Vibration", test = "Wald z", statistic = average$z.ratio,
        df = NA_real_, estimate = average$estimate, SE = average$SE,
        lower95 = average$asymp.LCL, upper95 = average$asymp.UCL,
        OR = average$OR, OR_lower95 = average$OR_lower95, OR_upper95 = average$OR_upper95,
        p_raw = average$p_raw
      )
      # 交互作用のみを除いたモデルとのLRT。同じデータ・ランダム効果を使用。
      additive <- fit_subjective(
        current, rating ~ stiffness + vibration + set + (1 + vc | subject), nAGQ = 1L
      )
      models[[question]]$additive <- additive
      diagnostics[[paste0(question, "_additive")]] <- subjective_diagnostics(additive, paste0(question, "_additive"))
      df <- attr(logLik(fit), "df") - attr(logLik(additive$model), "df")
      lr <- 2 * (as.numeric(logLik(fit)) - as.numeric(logLik(additive$model)))
      stopifnot(df == 2L, lr >= -1e-6, nobs(fit) == nobs(additive$model))
      interaction_p <- pchisq(max(0, lr), df, lower.tail = FALSE)
      tests[[paste0(question, "_interaction")]] <- data.frame(
        question, effect = "Interaction", test = "Likelihood-ratio chi-square",
        statistic = lr, df, estimate = NA_real_, SE = NA_real_,
        lower95 = NA_real_, upper95 = NA_real_, OR = NA_real_,
        OR_lower95 = NA_real_, OR_upper95 = NA_real_, p_raw = interaction_p
      )
    }

    # 交互作用有意なら剛性別Vib−Non。非有意なら有意な剛性主効果の3対比較。
    # Q3は剛性全体検定が有意な場合に3対比較を報告する。
    pair <- NULL
    if (!is.na(interaction_p) && interaction_p < alpha) {
      simple <- emmeans(fit, ~ vibration | stiffness, mode = "latent", weights = "equal", data = current)
      pair <- with_odds_ratio(contrast_summary(simple, vibration_contrast, holm = TRUE))
      pair$comparison <- "Vib_minus_Non_by_stiffness"
    } else if (stiffness_test$p < alpha) {
      pair <- with_odds_ratio(contrast_summary(stiffness_emm, stiffness_contrasts, holm = TRUE))
      pair$stiffness <- "average"
      pair$comparison <- "stiffness_pairs"
    }
    if (!is.null(pair)) {
      pair$question <- question
      posthoc[[question]] <- pair
    }

    scopes <- if (question == "Q3") list(stiffness = "stiffness") else list(
      overall = "vibration", by_stiffness = c("stiffness", "vibration"), stiffness = "stiffness"
    )
    for (scope in names(scopes)) {
      predicted <- expected_rating(fit, current, scopes[[scope]])
      predicted$question <- question
      predicted$scope <- scope
      predictions[[paste(question, scope)]] <- predicted[c(
        "question", "scope", "stiffness", "vibration", "estimate", "SE", "df", "asymp.LCL", "asymp.UCL"
      )]
    }
    # Set係数は推定値と未補正CIを保存する。
    set_terms <- grep("^set", names(fit$beta), value = TRUE)
    b <- fit$beta[set_terms]
    se <- sqrt(diag(vcov(fit)))[set_terms]
    sets[[question]] <- data.frame(question, term = set_terms, estimate = unname(b),
                                  SE = unname(se), lower95 = unname(b - qnorm(.975) * se),
                                  upper95 = unname(b + qnorm(.975) * se))
    cat(question, " CLMM 完了\n")
  }
  write_result(do.call(rbind, tests), "subjective_tests")
  if (length(posthoc)) write_result(do.call(rbind, posthoc), "subjective_posthoc")
  write_result(do.call(rbind, predictions), "subjective_expected_ratings")
  write_result(do.call(rbind, sets), "subjective_Set_coefficients")
  write_result(do.call(rbind, diagnostics), "subjective_diagnostics")
  saveRDS(models, file.path(output_dir, "subjective_models.rds"))
}
