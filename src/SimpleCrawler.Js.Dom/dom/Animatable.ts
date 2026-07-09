import { Animation } from "./Animation";

export interface Animatable {
    getAnimations(options?: GetAnimationsOptions): Animation[];
}

export interface GetAnimationsOptions {
    subtree?: boolean;
}