import { ICheckerSuite } from "ts-interface-checker";
import ICheckerStorage from "./checkerStorage";

export default function wrapCheckerStorage(suite: ICheckerSuite): ICheckerStorage {
    return {
        checker: (name) => {
            const result = suite[name]
            if (result === null || result === undefined) {
                throw Error(`No suitable checker for type ${name}`)
            }
            return result
        }
    }
}