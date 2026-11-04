# Numerical helpers retained from the selected SI2026 entry point.
alpha <- 0.05
stiffness_contrasts <- list(
  Mid_minus_Low = c(-1, 1, 0),
  High_minus_Low = c(-1, 0, 1),
  High_minus_Mid = c(0, -1, 1)
)
vibration_contrast <- list(Vib_minus_Non = c(-1, 1))

read_input <- function(name) {
  read.csv(file.path(input_dir, name), fileEncoding = "UTF-8-BOM",
           check.names = FALSE, na.strings = c("", "NA"))
}

write_result <- function(data, name) {
  write.csv(data, file.path(output_dir, paste0(name, ".csv")),
            row.names = FALSE, na = "", fileEncoding = "UTF-8")
}

# CIは未補正。Holm補正は各項目内の事後比較のp値にのみ適用する。
contrast_summary <- function(emm, contrasts, holm = FALSE) {
  result <- as.data.frame(summary(
    contrast(emm, contrasts, adjust = "none"),
    infer = c(TRUE, TRUE), level = 0.95, adjust = "none"
  ))
  names(result)[names(result) == "p.value"] <- "p_raw"
  if (holm) result$p_holm <- p.adjust(result$p_raw, method = "holm")
  result
}

# 警告・メッセージは表示を保ち、モデルと一緒に記録する。
fit_record <- function(expression) {
  warnings <- messages <- character()
  model <- withCallingHandlers(
    expression,
    warning = function(w) warnings <<- c(warnings, conditionMessage(w)),
    message = function(m) messages <<- c(messages, conditionMessage(m))
  )
  list(model = model, warnings = warnings, messages = messages)
}

trial_key <- function(data) paste(data$subject, data$set, data$trial, sep = "/")

