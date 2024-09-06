import { Checker } from "ts-interface-checker";

export default interface ICheckerStorage {
    checker(typeName: string): Checker
}