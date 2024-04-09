import { Checker } from "ts-interface-checker";

export interface INameToCheckerTable {
    checker(methodName: string): Checker | undefined
}

export function constNameCheckerTable(map: Map<string, Checker>): INameToCheckerTable {
    return {
        checker: (methodName: string) => {
            return map.get(methodName)
        }
    }
}