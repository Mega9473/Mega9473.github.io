# Only the libraries used by the analyses are attached.
load_dependencies <- function(root) {
  extra <- Sys.getenv("PORTFOLIO_R_EXTRA_LIB", unset = "")
  paths <- c(file.path(root, "r_library"), extra, .libPaths())
  .libPaths(unique(paths[nzchar(paths) & dir.exists(paths)]))
  packages <- c("ordinal", "lme4", "lmerTest", "emmeans", "dplyr", "tidyr",
                "readr", "tibble", "stringr", "ggplot2", "svglite", "jsonlite")
  missing <- packages[!vapply(packages, requireNamespace, logical(1), quietly = TRUE)]
  if (length(missing)) stop("Install missing R packages: ", paste(missing, collapse = ", "),
                            ". See install_dependencies.R.")
  suppressPackageStartupMessages(invisible(lapply(packages, library, character.only = TRUE)))
  library(grid)
  invisible(packages)
}

parse_cli <- function(args, defaults) {
  if (!length(args)) return(defaults)
  if (length(args) %% 2L) stop("Arguments must be --name value pairs.")
  for (index in seq.int(1L, length(args), by = 2L)) {
    key <- sub("^--", "", args[index])
    if (!startsWith(args[index], "--") || !key %in% names(defaults)) {
      stop("Unknown option: ", args[index])
    }
    defaults[[key]] <- args[index + 1L]
  }
  defaults
}

save_plot_pair <- function(plot_obj, file_base, width = 7.2, height = 5.4) {
  dir.create(dirname(file_base), recursive = TRUE, showWarnings = FALSE)
  ggplot2::ggsave(paste0(file_base, ".svg"), plot_obj, width = width, height = height,
                  device = svglite::svglite, bg = "white")
  ggplot2::ggsave(paste0(file_base, ".png"), plot_obj, width = width, height = height,
                  dpi = 180, bg = "white")
}

# All SI functions resolve their I/O in this environment, not in global state.
make_si_context <- function(root, input, output) {
  context <- new.env(parent = globalenv())
  context$input_dir <- normalizePath(input, winslash = "/", mustWork = TRUE)
  dir.create(output, recursive = TRUE, showWarnings = FALSE)
  context$output_dir <- normalizePath(output, winslash = "/", mustWork = TRUE)
  for (name in c("si_common.R", "si_inputs.R", "r2_objective_lmm.R", "r3_subjective_clmm.R")) {
    sys.source(file.path(root, "R", name), envir = context, keep.source = FALSE)
  }
  context
}
