import { ICheckerSuite } from "ts-interface-checker";
import ICheckerStorage from "./checkerStorage";

export default function wrapCheckerStorage(suite: ICheckerSuite): ICheckerStorage {
    return {
        checker: (name) => {
            return suite[name]
        }
    }
}