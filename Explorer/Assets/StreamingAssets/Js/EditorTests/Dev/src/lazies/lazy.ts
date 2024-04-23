interface Lazy<T> {
    value(): T
}

export function newLazy<T>(factoryFunction: () => T): Lazy<T> {
    let value: T | null = null

    return {
        value: () => {
            if (value === null) {
                value = factoryFunction()
            }
            return value
        }
    }
}