# Plots read only CSVs created by the current model run. No historical figure is used.
analysis_theme <- function() {
  theme_minimal(base_size = 12) +
    theme(panel.grid.minor = element_blank(), legend.position = "top",
          plot.title = element_text(face = "bold"),
          strip.text = element_text(face = "bold"),
          plot.caption = element_text(hjust = 0, size = 9))
}

read_result <- function(directory, name) {
  read.csv(file.path(directory, paste0(name, ".csv")), check.names = FALSE)
}

plot_r2 <- function(directory) {
  data <- read_result(directory, "objective_EMMs")
  data$stiffness <- factor(data$stiffness, levels = c("Low", "Mid", "High"))
  data$vibration <- factor(data$vibration, levels = 0:1, labels = c("Non", "Vib"))
  labels <- c(reaction = "Reaction force [N]", grip = "Grip force [N]",
              speed = "Speed [mm/s]", xmax = "Maximum position [mm]")
  data$metric <- factor(data$metric, levels = names(labels), labels = labels)
  position <- position_dodge(width = .18)
  plot <- ggplot(data, aes(stiffness, emmean, color = vibration, group = vibration)) +
    geom_line(position = position, linewidth = .8) +
    geom_point(position = position, size = 2.8) +
    geom_errorbar(aes(ymin = lower.CL, ymax = upper.CL), position = position, width = .10) +
    facet_wrap(~ metric, scales = "free_y", ncol = 2) +
    scale_color_manual(values = c(Non = "#64748B", Vib = "#C65D28")) +
    labs(title = "Objective outcomes: model-estimated means", x = "Stiffness", y = "Estimated marginal mean",
         color = "Vibration", caption = "REML LMM; Satterthwaite df. Error bars: unadjusted 95% CI.\n12 participants; 202 matched trials per outcome. Set is averaged with equal weights.") +
    analysis_theme()
  save_plot_pair(plot, file.path(directory, "figures", "R2_objective_EMMs"), 10, 7.5)

  rms <- read_result(directory, "RMS_descriptive")
  rms$stiffness <- factor(rms$stiffness, levels = c("Low", "Mid", "High"))
  plot <- ggplot(rms, aes(stiffness, mean_RMS)) +
    geom_point(size = 3.5, color = "#147D92") +
    geom_errorbar(aes(ymin = mean_RMS - SD, ymax = mean_RMS + SD), width = .15, color = "#147D92") +
    labs(title = "Vibration RMS by stiffness", x = "Stiffness", y = "RMS [source-data units]",
         caption = "Descriptive mean +/- SD across participant means; not an EMM confidence interval.\nVib trials only; 12 participants, 100 trials. Original RMS preprocessing is retained.") + analysis_theme()
  save_plot_pair(plot, file.path(directory, "figures", "R2_RMS_descriptive"), 7.2, 5.4)
}

plot_r3 <- function(directory) {
  all <- read_result(directory, "subjective_expected_ratings")
  data <- all[all$scope == "by_stiffness", ]
  data$stiffness <- factor(data$stiffness, levels = 2:4, labels = c("Low", "Mid", "High"))
  data$vibration <- factor(data$vibration, levels = 1:2, labels = c("Non", "Vib"))
  position <- position_dodge(width = .18)
  plot <- ggplot(data, aes(stiffness, estimate, color = vibration, group = vibration)) +
    geom_line(position = position, linewidth = .8) + geom_point(position = position, size = 3) +
    geom_errorbar(aes(ymin = asymp.LCL, ymax = asymp.UCL), position = position, width = .10) +
    facet_wrap(~question, nrow = 1) +
    scale_color_manual(values = c(Non = "#64748B", Vib = "#C65D28")) +
    scale_y_continuous(breaks = 1:7) + coord_cartesian(ylim = c(1, 7)) +
    labs(title = "Ordinal ratings: CLMM predictions", x = "Stiffness", y = "Expected rating E[Y]", color = "Vibration",
         caption = "E[Y] = sum(k x P[Y=k]); random effects set to zero. Set averaged equally.\nUnadjusted 95% CI; Q1/Q2 each 214 trials, 12 participants. Predictions are not raw rating means.") + analysis_theme()
  save_plot_pair(plot, file.path(directory, "figures", "R3_Q1_Q2_predictions"), 9.2, 5.5)

  data <- all[all$question == "Q3" & all$scope == "stiffness", ]
  data$stiffness <- factor(data$stiffness, levels = 2:4, labels = c("Low", "Mid", "High"))
  plot <- ggplot(data, aes(stiffness, estimate)) + geom_point(size = 3.5, color = "#147D92") +
    geom_errorbar(aes(ymin = asymp.LCL, ymax = asymp.UCL), width = .12, color = "#147D92") +
    scale_y_continuous(breaks = 1:7) + coord_cartesian(ylim = c(1, 7)) +
    labs(title = "Q3: predicted rating under vibration", x = "Stiffness", y = "Expected rating E[Y]",
         caption = "CLMM with random intercept; adaptive quadrature nAGQ=9.\nUnadjusted 95% CI; 106 Vib trials, 12 participants. Random effect = 0; Set averaged equally.") + analysis_theme()
  save_plot_pair(plot, file.path(directory, "figures", "R3_Q3_predictions"), 7.2, 5.4)
}
