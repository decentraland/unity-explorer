export interface Logger {
    warning(message: string): void
    error(message: string): void
    log(message: string): void
}

export function newLogger(
    logWarning: (message: string) => void,
    logError: (message: string) => void,
    log: (message: string) => void
): Logger {
    return {
        warning: logWarning,
        error: logError,
        log: log
    }
}